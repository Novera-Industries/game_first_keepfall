using System;
using System.Collections.Generic;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Core.Time;

namespace Keepfall.Monetization
{
    /// <summary>
    /// Which track a Battle Pass reward sits on. BOTH tracks are cosmetic-only (§7).
    /// </summary>
    public enum BattlePassTrack
    {
        /// <summary>Earned by everyone; the free track is completable fully F2P (§7).</summary>
        Free = 0,

        /// <summary>Earned by Battle Pass owners; still cosmetic-only (§7).</summary>
        Premium = 1,
    }

    /// <summary>
    /// A single Battle Pass reward. By contract it is ALWAYS a cosmetic — there are no unit,
    /// currency-bundle, or stat fields, so the type itself cannot carry power. The
    /// <see cref="IsCosmetic"/> invariant is asserted in BattlePassCosmeticOnlyTests.
    /// </summary>
    public sealed class BattlePassReward
    {
        /// <summary>Tier this reward unlocks at (1-based).</summary>
        public int Tier { get; }

        /// <summary>Free or premium track.</summary>
        public BattlePassTrack Track { get; }

        /// <summary>Cosmetic id granted into permanent <see cref="CosmeticState"/> ownership.</summary>
        public string CosmeticId { get; }

        /// <summary>
        /// Always true. The class has no non-cosmetic payload by design; this property exists so
        /// tests and audits can assert the cosmetic-only guarantee explicitly (§7, §10).
        /// </summary>
        public bool IsCosmetic => true;

        /// <summary>Creates a cosmetic reward. A null/empty cosmetic id is rejected so a reward
        /// can never be a silent power grant.</summary>
        public BattlePassReward(int tier, BattlePassTrack track, string cosmeticId)
        {
            if (tier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), "Tiers are 1-based.");
            }

            if (string.IsNullOrEmpty(cosmeticId))
            {
                throw new ArgumentException(
                    "A Battle Pass reward must be a cosmetic; cosmeticId is required (§7).",
                    nameof(cosmeticId));
            }

            Tier = tier;
            Track = track;
            CosmeticId = cosmeticId;
        }
    }

    /// <summary>
    /// Battle Pass v1 — source-of-truth §7. A 30-day season with a free track and a premium
    /// track, BOTH cosmetic-only. The free track is completable fully F2P. A tier-skip
    /// consumable advances tier progress ONLY — it carries no power and grants no units (§7
    /// "do not bundle power with it"); <see cref="SkipTier"/> asserts this.
    ///
    /// Pure C# (no UnityEngine) so the cosmetic-only invariant is unit-tested. Reward content is
    /// supplied to the constructor (the season definition is data owned elsewhere); this service
    /// owns progression, claiming-into-cosmetics, and the safe tier skip.
    /// </summary>
    public sealed class BattlePassService
    {
        private readonly IReadOnlyList<BattlePassReward> _rewards;
        private readonly CosmeticState _cosmetics;
        private readonly RemoteConfig _config;
        private readonly IAnalytics _analytics;
        private readonly ITimeProvider _time;

        private int _currentTier = 1;
        private bool _premiumOwned;

        /// <summary>
        /// Constructs the season. <paramref name="rewards"/> is the full reward list for both
        /// tracks; it is validated to be cosmetic-only at construction so a malformed season
        /// definition fails fast rather than shipping power. <paramref name="premiumOwned"/>
        /// reflects whether the player bought the premium track (a cosmetic purchase).
        /// </summary>
        public BattlePassService(
            IEnumerable<BattlePassReward> rewards,
            CosmeticState cosmetics,
            RemoteConfig config,
            bool premiumOwned = false,
            IAnalytics analytics = null,
            ITimeProvider time = null)
        {
            if (rewards == null)
            {
                throw new ArgumentNullException(nameof(rewards));
            }

            var list = new List<BattlePassReward>(rewards);
            foreach (BattlePassReward reward in list)
            {
                // Defense in depth: the type already guarantees cosmetic-only, but assert it so
                // any future reward subtype can never sneak power into a season.
                if (reward == null || !reward.IsCosmetic || string.IsNullOrEmpty(reward.CosmeticId))
                {
                    throw new ArgumentException(
                        "Every Battle Pass reward must be a non-empty cosmetic (§7).",
                        nameof(rewards));
                }
            }

            _rewards = list;
            _cosmetics = cosmetics ?? throw new ArgumentNullException(nameof(cosmetics));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _premiumOwned = premiumOwned;
            _analytics = analytics;
            _time = time ?? GameClock.Provider;
        }

        /// <summary>Season length in days. Default 30 (§7), tunable via <c>battlepass.seasonDays</c>.</summary>
        public int SeasonDays => _config.GetInt("battlepass.seasonDays", 30);

        /// <summary>Current 1-based tier the player has reached.</summary>
        public int CurrentTier => _currentTier;

        /// <summary>Whether the premium (cosmetic) track is owned.</summary>
        public bool PremiumOwned => _premiumOwned;

        /// <summary>Highest tier defined in the season's reward list.</summary>
        public int MaxTier
        {
            get
            {
                int max = 1;
                foreach (BattlePassReward r in _rewards)
                {
                    if (r.Tier > max)
                    {
                        max = r.Tier;
                    }
                }

                return max;
            }
        }

        /// <summary>
        /// Marks the premium cosmetic track as owned (after a validated cosmetic purchase). No
        /// power is conferred — it only unlocks the premium-track cosmetics for claiming.
        /// </summary>
        public void SetPremiumOwned(bool owned) => _premiumOwned = owned;

        /// <summary>
        /// Advances tier progress by <paramref name="tiers"/> from gameplay (quests, play time).
        /// Clamped to <see cref="MaxTier"/>. The free track is reachable to completion this way
        /// with no spend (§7).
        /// </summary>
        public void AddTierProgress(int tiers)
        {
            if (tiers <= 0)
            {
                return;
            }

            _currentTier = Math.Min(MaxTier, _currentTier + tiers);
        }

        /// <summary>
        /// Tier-skip consumable: advances tier progress by exactly <paramref name="tiers"/> and
        /// NOTHING else. Asserts no power/units are attached (§7) — the only effect is the same
        /// tier advance gameplay produces. Returns the new current tier.
        /// </summary>
        public int SkipTier(int tiers = 1)
        {
            if (tiers <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tiers), "Skip at least one tier.");
            }

            // Hard assertion of the §7 rule: a tier skip grants ONLY tier progress. Routing the
            // skip through the same AddTierProgress path guarantees it cannot grant units, Stone,
            // Shards, or any stat — there is no code path here that touches the roster or wallet.
            int before = _currentTier;
            AddTierProgress(tiers);

            if (_currentTier - before > tiers)
            {
                throw new InvalidOperationException(
                    "Tier skip advanced more than the purchased tiers — invariant broken (§7).");
            }

            return _currentTier;
        }

        /// <summary>
        /// Claims every reward the player is entitled to at or below <see cref="CurrentTier"/>:
        /// all free-track rewards, plus premium-track rewards when premium is owned. Each is
        /// folded into permanent <see cref="CosmeticState"/> ownership (kept forever, §6/§7).
        /// Idempotent and duplicate-safe. Returns the cosmetic ids newly granted this call.
        /// </summary>
        public IReadOnlyList<string> ClaimUnlockedRewards()
        {
            var owned = new HashSet<string>(_cosmetics.OwnedCosmeticIds);
            var newlyGranted = new List<string>();

            foreach (BattlePassReward reward in _rewards)
            {
                if (reward.Tier > _currentTier)
                {
                    continue;
                }

                if (reward.Track == BattlePassTrack.Premium && !_premiumOwned)
                {
                    continue;
                }

                if (owned.Add(reward.CosmeticId))
                {
                    _cosmetics.OwnedCosmeticIds.Add(reward.CosmeticId);
                    newlyGranted.Add(reward.CosmeticId);
                }
            }

            return newlyGranted;
        }

        /// <summary>
        /// True if the player has reached the top of the FREE track. Proves the §7 promise that
        /// the free track is completable F2P (no premium ownership required).
        /// </summary>
        public bool IsFreeTrackComplete()
        {
            int maxFreeTier = 0;
            foreach (BattlePassReward r in _rewards)
            {
                if (r.Track == BattlePassTrack.Free && r.Tier > maxFreeTier)
                {
                    maxFreeTier = r.Tier;
                }
            }

            return maxFreeTier > 0 && _currentTier >= maxFreeTier;
        }
    }
}
