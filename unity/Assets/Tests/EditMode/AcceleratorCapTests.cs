using System;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// THE REQUIRED accelerator test (source-of-truth §6 Product 1). Locks in every hard cap:
    /// refuses below 30% fill, refuses in the first 15 minutes of D1, fills to cap only (never
    /// beyond one full cap / ≤ 1 day of yield), refuses stacking past 3 queued days, and prices
    /// 15 / 30 / 60 Shards for T1 / T2 / T3.
    /// </summary>
    public sealed class AcceleratorCapTests
    {
        private static readonly DateTimeOffset D1Start =
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        private static (YieldAccelerator accel, Wallet wallet, FakeTimeProvider clock)
            BuildAccelerator(long shards = 1000)
        {
            var clock = new FakeTimeProvider(D1Start);
            var wallet = new Wallet(new WalletState(0, shards));
            var config = new RemoteConfig(); // canonical defaults via the typed getters
            var accel = new YieldAccelerator(wallet, config, analytics: null, time: clock);
            return (accel, wallet, clock);
        }

        private static TileState NewTile(string id, TileRank rank, long accrued, DateTimeOffset anchor)
        {
            // Anchor accrual to NOW so the test controls fill exactly via AccruedStone and does
            // not let wall-clock add yield between construction and the assertion.
            return new TileState(id, rank, anchor) { AccruedStone = accrued, LastAccrualUtc = anchor };
        }

        // ── 30% fill gate ────────────────────────────────────────────────

        [Test]
        public void CanOffer_RefusesBelowThirtyPercentFilled()
        {
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30)); // clear the D1 lock

            // T1 cap is 120; 30% = 36. 35 is below the gate.
            var tile = NewTile("01", TileRank.T1, accrued: 35, anchor: clock.UtcNow);

            AccelerateOffer offer = accel.CanOffer(tile, D1Start);

            Assert.IsFalse(offer.CanOffer);
            Assert.AreEqual(AccelerateRefusal.BelowMinFill, offer.Refusal);
        }

        [Test]
        public void CanOffer_AllowsAtThirtyPercentFilled()
        {
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30));

            // 36 == ceil(120 * 0.30) — exactly at the threshold.
            var tile = NewTile("01", TileRank.T1, accrued: 36, anchor: clock.UtcNow);

            AccelerateOffer offer = accel.CanOffer(tile, D1Start);

            Assert.IsTrue(offer.CanOffer, offer.Message);
            Assert.AreEqual(AccelerateRefusal.None, offer.Refusal);
        }

        // ── First 15 minutes of D1 lock ──────────────────────────────────

        [Test]
        public void CanOffer_RefusesDuringFirstFifteenMinutesOfD1()
        {
            var (accel, _, clock) = BuildAccelerator();
            // 14 minutes after first D1 play — still locked.
            clock.Advance(TimeSpan.FromMinutes(14));

            var tile = NewTile("01", TileRank.T1, accrued: 120, anchor: clock.UtcNow);

            AccelerateOffer offer = accel.CanOffer(tile, D1Start);

            Assert.IsFalse(offer.CanOffer);
            Assert.AreEqual(AccelerateRefusal.D1EarlyLock, offer.Refusal);
        }

        [Test]
        public void CanOffer_AllowsAfterFifteenMinutesOfD1()
        {
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(15)); // exactly at the lock boundary

            // 80/120 is above 30% and below cap.
            var tile = NewTile("01", TileRank.T1, accrued: 80, anchor: clock.UtcNow);

            AccelerateOffer offer = accel.CanOffer(tile, D1Start);

            Assert.IsTrue(offer.CanOffer, offer.Message);
        }

        // ── Fills to cap only (never beyond one full cap / ≤ 1 day) ──────

        [Test]
        public void ApplyAccelerate_FillsToCapExactly_NeverBeyond()
        {
            var (accel, wallet, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30));

            // T2 cap 300; start at 200 (above 30% = 90).
            var tile = NewTile("07", TileRank.T2, accrued: 200, anchor: clock.UtcNow);

            AccelerateResult result = accel.ApplyAccelerate(tile, D1Start);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(300, tile.AccruedStone, "Tile must be filled to its cap, not beyond.");
            Assert.AreEqual(100, result.StoneAdded, "Adds only the gap to cap (300 - 200).");

            // The deposit (100) is far less than one full cap (300), so a single purchase can
            // never exceed one cap fill — the §6 "≤ 1 day of yield per purchase" guarantee.
            Assert.LessOrEqual(result.StoneAdded, 300);
        }

        [Test]
        public void ApplyAccelerate_DoesNotDoubleCount_OnSecondCallSameInstant()
        {
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30));

            var tile = NewTile("07", TileRank.T2, accrued: 200, anchor: clock.UtcNow);
            accel.ApplyAccelerate(tile, D1Start); // -> 300, anchor re-set to now

            // Immediately re-offering: tile is at cap, nothing to fill.
            AccelerateOffer offer = accel.CanOffer(tile, D1Start);
            Assert.IsFalse(offer.CanOffer);
            Assert.AreEqual(AccelerateRefusal.AlreadyAtCap, offer.Refusal);
        }

        // ── No stacking past 3 queued days ───────────────────────────────

        [Test]
        public void CanOffer_RefusesWhenFilledTileWouldExceedThreeQueuedDays()
        {
            // Construct a config where a single cap fill represents MORE than 3 days of yield,
            // so the stacking guard must refuse. T3 default: cap 720, yield 60/hr -> 0.5 day.
            // Override the cap to 6000 (6000/60/24 = 4.17 days) while keeping the yield, so a
            // full fill would queue > 3 days.
            var clock = new FakeTimeProvider(D1Start);
            var wallet = new Wallet(new WalletState(0, 1000));
            var config = new RemoteConfig(
                "{ \"tile.cap.t3\": 6000, \"tile.yield.t3\": 60 }");
            var accel = new YieldAccelerator(wallet, config, analytics: null, time: clock);
            clock.Advance(TimeSpan.FromMinutes(30));

            // 30% of 6000 = 1800; start above it so the only refusal is the queued-days cap.
            var tile = NewTile("t3", TileRank.T3, accrued: 2000, anchor: clock.UtcNow);

            AccelerateOffer offer = accel.CanOffer(tile, D1Start);

            Assert.IsFalse(offer.CanOffer);
            Assert.AreEqual(AccelerateRefusal.WouldExceedQueuedDays, offer.Refusal);
        }

        [Test]
        public void CanOffer_AllowsWhenQueuedYieldStaysWithinThreeDays()
        {
            // Canonical T3: cap 720, 60/hr -> 0.5 day of queued yield at full. Well within 3.
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30));

            var tile = NewTile("t3", TileRank.T3, accrued: 400, anchor: clock.UtcNow); // > 30% of 720
            AccelerateOffer offer = accel.CanOffer(tile, D1Start);

            Assert.IsTrue(offer.CanOffer, offer.Message);
        }

        // ── Correct price per rank ───────────────────────────────────────

        [Test]
        public void CanOffer_PricesFifteenThirtySixtyByRank()
        {
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30));

            var t1 = NewTile("a", TileRank.T1, accrued: 60, anchor: clock.UtcNow);   // > 30% of 120
            var t2 = NewTile("b", TileRank.T2, accrued: 150, anchor: clock.UtcNow);  // > 30% of 300
            var t3 = NewTile("c", TileRank.T3, accrued: 400, anchor: clock.UtcNow);  // > 30% of 720

            Assert.AreEqual(15, accel.CanOffer(t1, D1Start).PriceShards, "T1 = 15 Shards.");
            Assert.AreEqual(30, accel.CanOffer(t2, D1Start).PriceShards, "T2 = 30 Shards.");
            Assert.AreEqual(60, accel.CanOffer(t3, D1Start).PriceShards, "T3 = 60 Shards.");
        }

        // ── Emits analytics on a successful accelerate ───────────────────

        [Test]
        public void ApplyAccelerate_EmitsAcceleratorUsedEvent()
        {
            var clock = new FakeTimeProvider(D1Start);
            var wallet = new Wallet(new WalletState(0, 1000));
            var analytics = new RecordingAnalytics();
            var accel = new YieldAccelerator(wallet, new RemoteConfig(), analytics, clock);
            clock.Advance(TimeSpan.FromMinutes(30));

            var tile = NewTile("07", TileRank.T2, accrued: 200, anchor: clock.UtcNow);
            AccelerateResult result = accel.ApplyAccelerate(tile, D1Start);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, analytics.CountOf(Events.AcceleratorUsed),
                "A successful accelerate emits exactly one purchase event.");
            // No celebratory/modal event exists for an accelerate — the only event is the purchase.
            Assert.AreEqual(1, analytics.Events.Count, "No extra (e.g. confetti) events fire.");
        }

        [Test]
        public void ApplyAccelerate_RefusalEmitsNoAnalytics()
        {
            var clock = new FakeTimeProvider(D1Start);
            var wallet = new Wallet(new WalletState(0, 1000));
            var analytics = new RecordingAnalytics();
            var accel = new YieldAccelerator(wallet, new RemoteConfig(), analytics, clock);
            // Still inside the D1 lock — the accelerate is refused.
            clock.Advance(TimeSpan.FromMinutes(5));

            var tile = NewTile("07", TileRank.T2, accrued: 200, anchor: clock.UtcNow);
            AccelerateResult result = accel.ApplyAccelerate(tile, D1Start);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, analytics.Events.Count, "A refused accelerate emits nothing.");
        }

        // ── Plus subscriber fills to the EFFECTIVE (+50%) cap, not the base cap ──

        [Test]
        public void ApplyAccelerate_PlusSubscriber_FillsToScaledCap()
        {
            var (accel, _, clock) = BuildAccelerator();
            clock.Advance(TimeSpan.FromMinutes(30));

            // T1 base cap 120; with Plus (+50%) the effective cap is 180. Start at 100 (above the
            // 30% gate of 180 = 54) and accelerate as a subscriber.
            var tile = NewTile("01", TileRank.T1, accrued: 100, anchor: clock.UtcNow);

            AccelerateResult result = accel.ApplyAccelerate(tile, D1Start, subscriptionActive: true);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(180, tile.AccruedStone,
                "A Plus subscriber fills to the +50% effective cap (120 -> 180).");
            Assert.AreEqual(80, result.StoneAdded, "Adds the gap to the scaled cap (180 - 100).");
        }

        [Test]
        public void ApplyAccelerate_ChargesExactRankPrice_AndRefusesWhenBroke()
        {
            // Wallet has only 14 Shards — one short of the T1 price (15).
            var clock = new FakeTimeProvider(D1Start);
            var wallet = new Wallet(new WalletState(0, 14));
            var config = new RemoteConfig();
            var accel = new YieldAccelerator(wallet, config, analytics: null, time: clock);
            clock.Advance(TimeSpan.FromMinutes(30));

            var tile = NewTile("a", TileRank.T1, accrued: 60, anchor: clock.UtcNow);

            AccelerateResult result = accel.ApplyAccelerate(tile, D1Start);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AccelerateRefusal.InsufficientShards, result.Refusal);
            Assert.AreEqual(14, wallet.GetBalance(CurrencyType.Shards), "No charge on refusal.");
            Assert.AreEqual(60, tile.AccruedStone, "Tile unchanged on refusal.");

            // Top up to exactly the price and retry: charges 15, leaving 14.
            wallet.Add(CurrencyType.Shards, 15); // now 29
            AccelerateResult ok = accel.ApplyAccelerate(tile, D1Start);
            Assert.IsTrue(ok.Success, ok.Message);
            Assert.AreEqual(15, ok.ShardsSpent);
            Assert.AreEqual(14, wallet.GetBalance(CurrencyType.Shards));
            Assert.AreEqual(120, tile.AccruedStone);
        }
    }
}
