using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;

namespace Keepfall.Monetization
{
    /// <summary>
    /// Keepfall Plus — source-of-truth §6 Product 2. EXACTLY ONE tier, $5.99/month, StoreKit 2
    /// auto-renewable, with an optional 7-day free trial gated by the remote-config flag
    /// <c>plus.trial.enabled</c>. Multi-tier subscriptions are an anti-pattern (§10.8), so there
    /// is no tier parameter anywhere in this type.
    ///
    /// <para><b>Perks (all convenience or cosmetic, never power):</b> +50% tile yield (read by
    /// the Economy assembly), +1 deck slot, 2× daily-quest Shards, +5 login Shards, a monthly
    /// cosmetic drop, and 1 free Battle Pass tier skip per week.</para>
    ///
    /// <para><b>Hard exclusions enforced in code + tests (PlusExclusionTests):</b> no
    /// subscriber-only units (<see cref="IsUnitSubscriberLocked"/> is always false), no
    /// subscriber-only tiles (<see cref="IsTileSubscriberLocked"/> is always false), no PvP
    /// perks (inert placeholder), and NO combat advantage of any kind
    /// (<see cref="ModifiesCombatStats"/> is always false).</para>
    ///
    /// <para><b>Trust commitment (§6):</b> cosmetics earned during the subscription are KEPT on
    /// cancellation. The cancel path routes through
    /// <see cref="SubscriptionCosmetics.KeepCosmeticsOnCancellation"/> and never revokes.</para>
    ///
    /// Receipt validation is server-authoritative: subscription start/renewal flows through
    /// <see cref="IBackendClient.ValidateReceiptAsync"/>; the client only caches the verdict.
    /// </summary>
    public sealed class PlusSubscription
    {
        /// <summary>The single canonical StoreKit product id for Plus. ONE tier only (§6).</summary>
        public const string PlusProductId = "com.vyradata.keepfall.plus.monthly";

        private readonly SubscriptionState _state;
        private readonly CosmeticState _cosmetics;
        private readonly DeckState _deck;
        private readonly RemoteConfig _config;
        private readonly IBackendClient _backend;
        private readonly IAnalytics _analytics;
        private readonly ITimeProvider _time;

        /// <summary>
        /// Wraps the player's subscription/cosmetic/deck save state plus config and backend.
        /// </summary>
        public PlusSubscription(
            SubscriptionState state,
            CosmeticState cosmetics,
            DeckState deck,
            RemoteConfig config,
            IBackendClient backend,
            IAnalytics analytics = null,
            ITimeProvider time = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _cosmetics = cosmetics ?? throw new ArgumentNullException(nameof(cosmetics));
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _analytics = analytics;
            _time = time ?? GameClock.Provider;
        }

        // ── HARD EXCLUSION GUARDS (source-of-truth §6 + §10) ─────────────
        // These are intentionally constant. They exist so any caller — including future code —
        // gets a compile/runtime-visible "no" rather than an ad-hoc check, and so the test
        // suite can assert the exclusions can never drift. NEVER make any of these return true.

        /// <summary>Plus NEVER locks a unit behind the subscription (§6, §10.2). Always false.</summary>
        public static bool IsUnitSubscriberLocked(string unitId) => false;

        /// <summary>Plus NEVER locks a tile or tile rank behind the subscription (§6). Always false.</summary>
        public static bool IsTileSubscriberLocked(string tileId) => false;

        /// <summary>Plus NEVER grants a PvP perk — PvP is an inert Phase-2 placeholder (§0, §6).</summary>
        public static bool GrantsPvpPerk => false;

        /// <summary>Plus NEVER modifies any combat stat. Perks are yield/slots/economy only (§6).</summary>
        public static bool ModifiesCombatStats => false;

        // ── Active-state queries ─────────────────────────────────────────

        /// <summary>True if Plus is active AND the current period has not lapsed.</summary>
        public bool IsActive =>
            _state.Active && _state.CurrentPeriodEndUtc > _time.UtcNow;

        /// <summary>
        /// Whether the 7-day free trial may be offered: the remote-config flag must be enabled
        /// AND the player must not have already consumed a trial (one trial per player, §6).
        /// </summary>
        public bool CanOfferTrial() =>
            _config.GetPlusTrialEnabled() && !_state.TrialUsed;

        // ── Perk reads (convenience only) ────────────────────────────────

        /// <summary>
        /// Tile-yield multiplier the Economy assembly applies. 1.5 (+50%) while Plus is active,
        /// 1.0 otherwise. This is the ONLY way Plus touches yield — it compresses earned time,
        /// it does not change combat or grant currency directly.
        /// </summary>
        public double GetYieldMultiplier() =>
            IsActive ? _config.GetPlusYieldMultiplier() : 1.0;

        /// <summary>Total deck slots the player should have: base 3 + Plus bonus (4) while
        /// active. Purchased expansion (up to 6) is handled by the Shop, not here.</summary>
        public int GetEffectiveDeckSlots()
        {
            int baseSlots = DeckState.DefaultSlotsUnlocked;
            return IsActive ? baseSlots + _config.GetPlusExtraDeckSlots() : baseSlots;
        }

        /// <summary>Daily-quest Shard multiplier (2× while active, else 1×).</summary>
        public int GetDailyQuestShardMultiplier() =>
            IsActive ? _config.GetInt("plus.dailyQuestShardMultiplier", 2) : 1;

        /// <summary>Bonus Shards added to the daily login while active (+5, else 0).</summary>
        public int GetDailyLoginShardBonus() =>
            IsActive ? _config.GetInt("plus.dailyLoginShardBonus", 5) : 0;

        /// <summary>Free Battle Pass tier skips granted per week while active (1, else 0).</summary>
        public int GetFreeTierSkipsPerWeek() =>
            IsActive ? _config.GetInt("plus.freeTierSkipsPerWeek", 1) : 0;

        // ── Subscription lifecycle (server-authoritative) ────────────────

        /// <summary>
        /// Starts (or renews) Plus from a StoreKit 2 signed transaction. The receipt is
        /// validated server-side; only a valid verdict activates perks. When
        /// <paramref name="asTrial"/> is true the trial flag is honored only if
        /// <see cref="CanOfferTrial"/> currently allows it. Applies the +1 deck slot perk on
        /// success. Returns true if Plus is active after the call.
        /// </summary>
        public async Task<bool> StartOrRenewAsync(
            string signedTransaction,
            bool asTrial = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(signedTransaction))
            {
                throw new ArgumentException("A StoreKit 2 signed transaction is required.",
                    nameof(signedTransaction));
            }

            bool startingTrial = asTrial && CanOfferTrial();

            ValidateReceiptResponse verdict = await _backend.ValidateReceiptAsync(
                new ValidateReceiptRequest
                {
                    SignedTransaction = signedTransaction,
                    ProductId = PlusProductId,
                },
                cancellationToken);

            // The server is the authority. A failed validation activates nothing.
            if (verdict == null || !verdict.Valid)
            {
                return false;
            }

            _state.Active = true;
            _state.ProductId = string.IsNullOrEmpty(verdict.ProductId)
                ? PlusProductId
                : verdict.ProductId;
            _state.CurrentPeriodEndUtc = ParsePeriodEnd(verdict.CurrentPeriodEndUtc);

            if (startingTrial)
            {
                _state.TrialUsed = true;
                Track(Events.PlusTrialStart);
            }

            // Apply the +1 deck-slot convenience perk (never below current, never a power perk).
            ApplyDeckSlotPerk();

            Track(Events.PlusSubscribe);
            return IsActive;
        }

        /// <summary>
        /// Records a monthly cosmetic drop earned while subscribed. It is tracked in
        /// <see cref="SubscriptionState.CosmeticsGrantedDuringSub"/> so the cancel path can KEEP
        /// it permanently (§6). Cosmetic-only — confers no combat advantage. No-op if Plus is
        /// not active or the id is empty.
        /// </summary>
        public bool GrantMonthlyCosmetic(string cosmeticId)
        {
            if (!IsActive || string.IsNullOrEmpty(cosmeticId))
            {
                return false;
            }

            if (!_state.CosmeticsGrantedDuringSub.Contains(cosmeticId))
            {
                _state.CosmeticsGrantedDuringSub.Add(cosmeticId);
            }

            return true;
        }

        /// <summary>
        /// Cancels / lapses Plus. The NON-NEGOTIABLE trust commitment (§6): every cosmetic
        /// earned during the subscription is folded into permanent <see cref="CosmeticState"/>
        /// ownership and NOTHING is revoked. Deck slots return to the F2P baseline only if the
        /// player has not purchased extra slots — convenience lapses, owned content does not.
        /// </summary>
        public void Cancel()
        {
            // Keep cosmetics FOREVER, then mark inactive. Routed through Core's canonical,
            // duplicate-safe, idempotent migration — never clear or revoke here.
            SubscriptionCosmetics.KeepCosmeticsOnCancellation(_state, _cosmetics);

            // Lapse the convenience deck slot, but never drop below what the player owns.
            // F2P baseline is the floor; purchased expansions (handled by Shop) sit above it.
            if (_deck.SlotsUnlocked > DeckState.DefaultSlotsUnlocked)
            {
                int withoutPlus = _deck.SlotsUnlocked - _config.GetPlusExtraDeckSlots();
                _deck.SlotsUnlocked = Math.Max(DeckState.DefaultSlotsUnlocked, withoutPlus);
            }

            Track(Events.PlusCancel);
        }

        // ── Internals ────────────────────────────────────────────────────

        private void ApplyDeckSlotPerk()
        {
            int target = DeckState.DefaultSlotsUnlocked + _config.GetPlusExtraDeckSlots();
            if (_deck.SlotsUnlocked < target)
            {
                _deck.SlotsUnlocked = target;
            }
        }

        private DateTimeOffset ParsePeriodEnd(string iso)
        {
            if (!string.IsNullOrEmpty(iso) &&
                DateTimeOffset.TryParse(iso, out DateTimeOffset parsed))
            {
                return parsed.ToUniversalTime();
            }

            // No server-reported end (e.g. a trial start the receipt did not carry an end for):
            // fall back to a trial/month window so perks have a sane lapse anchor. The server
            // remains authoritative and will correct this on the next validation.
            int days = CanOfferTrial() ? _config.GetPlusTrialDays() : 30;
            return _time.UtcNow + TimeSpan.FromDays(days);
        }

        private void Track(string evt)
        {
            _analytics?.Track(evt, new Dictionary<string, object>
            {
                ["product_id"] = PlusProductId,
            });
        }
    }
}
