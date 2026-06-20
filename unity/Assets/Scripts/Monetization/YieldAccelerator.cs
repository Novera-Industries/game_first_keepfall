using System;
using System.Collections.Generic;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Economy;

namespace Keepfall.Monetization
{
    /// <summary>
    /// Why a tile cannot currently be accelerated. Drives calm UI copy
    /// (<see cref="MonetizationStrings"/>) and analytics, and keeps the hard caps from
    /// source-of-truth §6 Product 1 explicit and testable.
    /// </summary>
    public enum AccelerateRefusal
    {
        /// <summary>No refusal — the offer is valid.</summary>
        None = 0,

        /// <summary>Tile is below the 30% fill threshold the offer requires.</summary>
        BelowMinFill = 1,

        /// <summary>Within the first 15 minutes of D1 play (accelerator locked).</summary>
        D1EarlyLock = 2,

        /// <summary>Tile is already at/above its cap; one purchase fills to cap only, so
        /// there is nothing to fill.</summary>
        AlreadyAtCap = 3,

        /// <summary>Filling now would push queued yield past the 3-day stacking cap.</summary>
        WouldExceedQueuedDays = 4,

        /// <summary>Wallet cannot afford the Shard price.</summary>
        InsufficientShards = 5,
    }

    /// <summary>
    /// The decision describing whether a tile may be accelerated, what it would cost, and how
    /// much Stone the purchase would deposit. Pure data so the UI can render an offer (or a
    /// calm reason it is unavailable) without re-running the rules.
    /// </summary>
    public readonly struct AccelerateOffer
    {
        /// <summary>True only if every §6 hard cap passes and the wallet can afford it.</summary>
        public bool CanOffer { get; }

        /// <summary>Why the offer is unavailable (None when <see cref="CanOffer"/> is true).</summary>
        public AccelerateRefusal Refusal { get; }

        /// <summary>Shard price for this tile's rank (T1=15, T2=30, T3=60 by default, §6).</summary>
        public int PriceShards { get; }

        /// <summary>Stone the purchase would add (fills to CURRENT cap only — never beyond
        /// one full cap fill, §6).</summary>
        public long StoneToAdd { get; }

        /// <summary>Calm, source-of-truth §12-compliant message for the UI.</summary>
        public string Message { get; }

        internal AccelerateOffer(
            bool canOffer, AccelerateRefusal refusal, int priceShards, long stoneToAdd,
            string message)
        {
            CanOffer = canOffer;
            Refusal = refusal;
            PriceShards = priceShards;
            StoneToAdd = stoneToAdd;
            Message = message;
        }
    }

    /// <summary>
    /// Outcome of an <see cref="YieldAccelerator.ApplyAccelerate"/> call.
    /// </summary>
    public readonly struct AccelerateResult
    {
        /// <summary>True if Shards were spent and the tile was filled to cap.</summary>
        public bool Success { get; }

        /// <summary>Refusal reason when <see cref="Success"/> is false.</summary>
        public AccelerateRefusal Refusal { get; }

        /// <summary>Stone actually deposited into the tile (0 on failure).</summary>
        public long StoneAdded { get; }

        /// <summary>Shards actually spent (0 on failure).</summary>
        public int ShardsSpent { get; }

        /// <summary>Calm UI message describing the outcome.</summary>
        public string Message { get; }

        internal AccelerateResult(
            bool success, AccelerateRefusal refusal, long stoneAdded, int shardsSpent,
            string message)
        {
            Success = success;
            Refusal = refusal;
            StoneAdded = stoneAdded;
            ShardsSpent = shardsSpent;
            Message = message;
        }
    }

    /// <summary>
    /// Yield Accelerator — source-of-truth §6 Product 1. A consumable that fills ONE tile to
    /// its CURRENT cap instantly. It compresses time the player already earned; it never sells
    /// an outcome and never grants a tile (tiles come only from winning combat, §2).
    ///
    /// <para><b>Hard caps enforced here (each has a test in AcceleratorCapTests):</b></para>
    /// <list type="bullet">
    ///   <item>Tile must be ≥ 30% filled before the offer appears.</item>
    ///   <item>Locked during the first 15 minutes of D1 play.</item>
    ///   <item>One purchase fills to the cap only — never more than one full cap of yield
    ///   (≤ 1 day of yield, since a cap fills in 12h; a single purchase can never exceed it).</item>
    ///   <item>Refuses if the resulting queued yield would exceed 3 days for the tile.</item>
    ///   <item>Price 15 / 30 / 60 Shards for T1 / T2 / T3 (from remote config).</item>
    /// </list>
    ///
    /// Pure C# (no UnityEngine) so the caps are unit-tested in EditMode. All thresholds are read
    /// from <see cref="RemoteConfig"/> so they tune without a rebuild (§11); the defaults match
    /// the canonical numbers. The accrual math (rate × elapsed, clamped to cap) is owned by the
    /// Economy assembly: this service routes EVERY rate/cap/accrual read through
    /// <see cref="TileYield"/> so the EFFECTIVE cap (including the Keepfall Plus +50% scaling of
    /// both rate and cap, §6) is the one a fill-to-cap targets. A Plus subscriber therefore fills
    /// to their larger effective cap — never over- or under-shooting the Economy's view.
    /// </summary>
    public sealed class YieldAccelerator
    {
        private readonly Wallet _wallet;
        private readonly RemoteConfig _config;
        private readonly IAnalytics _analytics;
        private readonly ITimeProvider _time;

        /// <summary>
        /// Constructs the accelerator over the player's wallet and the live config. The time
        /// source defaults to <see cref="GameClock.Provider"/>; tests inject a
        /// <see cref="FakeTimeProvider"/>. Analytics may be null (no-op) in headless contexts.
        /// </summary>
        public YieldAccelerator(
            Wallet wallet,
            RemoteConfig config,
            IAnalytics analytics = null,
            ITimeProvider time = null)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _analytics = analytics;
            _time = time ?? GameClock.Provider;
        }

        /// <summary>
        /// Decides whether <paramref name="tile"/> may be accelerated right now and what the
        /// offer would be. Does not mutate state. <paramref name="firstD1PlayUtc"/> is when the
        /// player first started playing on D1 (their install/first-session anchor) — the offer
        /// is locked for the first <c>accelerator.lockedFirstMinutesD1</c> minutes after it.
        /// <paramref name="subscriptionActive"/> selects the EFFECTIVE rate/cap via
        /// <see cref="TileYield"/> (Plus scales both by +50%, §6), so a subscriber's fill targets
        /// their larger cap.
        /// </summary>
        public AccelerateOffer CanOffer(
            TileState tile, DateTimeOffset firstD1PlayUtc, bool subscriptionActive = false)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            DateTimeOffset now = _time.UtcNow;
            int price = _config.GetAcceleratorPrice(tile.Rank);

            // Effective rate/cap from the canonical Economy math (includes Plus +50% scaling).
            double yieldPerHour = TileYield.RatePerHour(_config, tile.Rank, subscriptionActive);
            long cap = TileYield.Cap(_config, tile.Rank, subscriptionActive);

            // 1. D1 early lock — never offer in the first 15 minutes of D1 play.
            int lockMinutes = _config.GetAcceleratorD1LockMinutes();
            if (now < firstD1PlayUtc + TimeSpan.FromMinutes(lockMinutes))
            {
                return Refuse(AccelerateRefusal.D1EarlyLock, price,
                    MonetizationStrings.AcceleratorLockedEarly);
            }

            // Current accrued Stone, computed exactly as the Economy assembly does (rate × elapsed,
            // clamped to the effective cap) so the accelerator never diverges from the live tile.
            long accrued = TileYield.AccrueTo(_config, tile, subscriptionActive, now);

            // 2. ≥ 30% fill required before the offer appears.
            double minFill = _config.GetAcceleratorMinFillPercentToShow();
            if (cap <= 0 || accrued < (long)Math.Ceiling(cap * minFill))
            {
                return Refuse(AccelerateRefusal.BelowMinFill, price,
                    MonetizationStrings.AcceleratorNeedsMoreFill);
            }

            // 3. Already at/above cap — a fill-to-cap purchase would deposit nothing.
            long stoneToAdd = cap - accrued;
            if (stoneToAdd <= 0)
            {
                return Refuse(AccelerateRefusal.AlreadyAtCap, price,
                    MonetizationStrings.AcceleratorAlreadyAtCap);
            }

            // 4. No stacking past 3 queued days. "Queued yield" = the Stone that would sit on
            //    the tile after the fill, measured in days of this rank's yield. Filling to cap
            //    must not push that beyond accelerator.maxQueuedDays days. (Plus scales rate
            //    and cap together, so this days-to-fill ratio is the same with or without Plus.)
            int maxDays = _config.GetAcceleratorMaxDaysQueued();
            if (yieldPerHour > 0)
            {
                double queuedDaysAfterFill = cap / yieldPerHour / 24.0;
                if (queuedDaysAfterFill > maxDays + 1e-9)
                {
                    return Refuse(AccelerateRefusal.WouldExceedQueuedDays, price,
                        MonetizationStrings.AcceleratorWouldStackTooFar);
                }
            }

            // 5. Affordability (a soft gate — the offer can still be shown, but flagged).
            if (!_wallet.CanAfford(CurrencyType.Shards, price))
            {
                return new AccelerateOffer(false, AccelerateRefusal.InsufficientShards,
                    price, stoneToAdd, MonetizationStrings.AcceleratorNotEnoughShards);
            }

            return new AccelerateOffer(true, AccelerateRefusal.None, price, stoneToAdd,
                MonetizationStrings.AcceleratorFilled);
        }

        /// <summary>
        /// Charges the Shard price and fills <paramref name="tile"/> to its CURRENT effective cap.
        /// Re-runs <see cref="CanOffer"/> as the authority (never trust a stale UI offer) and
        /// refuses if any §6 cap fails or the wallet cannot pay. On success, sets accrued to the
        /// effective cap, advances the accrual anchor so no double-counting occurs, and emits an
        /// analytics event. <paramref name="subscriptionActive"/> selects the effective cap.
        /// </summary>
        public AccelerateResult ApplyAccelerate(
            TileState tile, DateTimeOffset firstD1PlayUtc, bool subscriptionActive = false)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            AccelerateOffer offer = CanOffer(tile, firstD1PlayUtc, subscriptionActive);
            if (!offer.CanOffer)
            {
                return new AccelerateResult(false, offer.Refusal, 0, 0, offer.Message);
            }

            // Charge first; if the debit fails (race against another spend), abort cleanly.
            if (!_wallet.TrySpend(CurrencyType.Shards, offer.PriceShards))
            {
                return new AccelerateResult(false, AccelerateRefusal.InsufficientShards, 0, 0,
                    MonetizationStrings.AcceleratorNotEnoughShards);
            }

            DateTimeOffset now = _time.UtcNow;
            long cap = TileYield.Cap(_config, tile.Rank, subscriptionActive);
            long before = TileYield.AccrueTo(_config, tile, subscriptionActive, now);
            long added = cap - before;

            // Fill to the effective cap only (≤ one full cap fill — the §6 "≤ 1 day of yield"
            // guarantee, since a cap fills in 12h). Re-anchor accrual to NOW so future real-time
            // accrual measures from a full tile and never re-credits the time we just filled.
            tile.AccruedStone = cap;
            tile.LastAccrualUtc = now;

            Track(tile, offer.PriceShards, added);

            return new AccelerateResult(true, AccelerateRefusal.None, added, offer.PriceShards,
                MonetizationStrings.AcceleratorFilled);
        }

        // ── Internals ────────────────────────────────────────────────────

        private static AccelerateOffer Refuse(
            AccelerateRefusal reason, int price, string message)
        {
            return new AccelerateOffer(false, reason, price, 0, message);
        }

        private void Track(TileState tile, int priceShards, long stoneAdded)
        {
            _analytics?.Track(Events.AcceleratorUsed, new Dictionary<string, object>
            {
                ["tile_id"] = tile.Id,
                ["tile_rank"] = tile.Rank.ToString(),
                ["price_shards"] = priceShards,
                ["stone_added"] = stoneAdded,
            });
        }
    }
}
