using System;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Economy;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Pure accrual-math checks for <see cref="TileYield"/> (source-of-truth §2 yield table and
    /// §6 Keepfall Plus +50%). Uses an empty <see cref="RemoteConfig"/> so the canonical fallback
    /// values (T1 10/120, T2 25/300, T3 60/720; Plus ×1.5) are exercised exactly as shipped.
    /// </summary>
    public sealed class TileYieldTests
    {
        private RemoteConfig _config;

        [SetUp]
        public void SetUp()
        {
            // No defaults loaded -> RemoteConfig returns its hard-coded canonical fallbacks,
            // which mirror source-of-truth §2/§6.
            _config = new RemoteConfig();
        }

        // ── Per-rank rate and cap (§2) ───────────────────────────────────

        [TestCase(TileRank.T1, 10.0, 120)]
        [TestCase(TileRank.T2, 25.0, 300)]
        [TestCase(TileRank.T3, 60.0, 720)]
        public void RateAndCap_MatchCanonicalTable_WhenNoSubscription(
            TileRank rank, double expectedRate, long expectedCap)
        {
            Assert.AreEqual(expectedRate, TileYield.RatePerHour(_config, rank, false), 1e-9);
            Assert.AreEqual(expectedCap, TileYield.Cap(_config, rank, false));
        }

        [TestCase(TileRank.T1, 1.0, 10)]
        [TestCase(TileRank.T2, 1.0, 25)]
        [TestCase(TileRank.T3, 1.0, 60)]
        [TestCase(TileRank.T1, 5.0, 50)]
        [TestCase(TileRank.T2, 4.0, 100)]
        public void Accrue_OverWholeHours_IsRateTimesHours(
            TileRank rank, double hours, long expected)
        {
            long got = TileYield.Accrue(_config, rank, false, 0, TimeSpan.FromHours(hours));
            Assert.AreEqual(expected, got);
        }

        // ── 12-hour cap (§2: every rank fills its cap in 12 hours) ───────

        [TestCase(TileRank.T1, 120)]
        [TestCase(TileRank.T2, 300)]
        [TestCase(TileRank.T3, 720)]
        public void Accrue_AtExactly12Hours_HitsCap(TileRank rank, long expectedCap)
        {
            long got = TileYield.Accrue(_config, rank, false, 0, TimeSpan.FromHours(12));
            Assert.AreEqual(expectedCap, got);
        }

        [TestCase(TileRank.T1, 120)]
        [TestCase(TileRank.T2, 300)]
        [TestCase(TileRank.T3, 720)]
        public void Accrue_PastCap_ClampsToCap(TileRank rank, long expectedCap)
        {
            // 100 hours is far past the 12h fill; must clamp, never overshoot.
            long got = TileYield.Accrue(_config, rank, false, 0, TimeSpan.FromHours(100));
            Assert.AreEqual(expectedCap, got);
        }

        [Test]
        public void Accrue_WhenAlreadyAtCap_AddsNothing()
        {
            long got = TileYield.Accrue(_config, TileRank.T1, false, 120, TimeSpan.FromHours(5));
            Assert.AreEqual(120, got);
        }

        // ── Partial-hour accrual ─────────────────────────────────────────

        [Test]
        public void Accrue_PartialHour_FloorsToWholeStone()
        {
            // T2 = 25/hr. 30 minutes -> 12.5 Stone -> floored to 12.
            long got = TileYield.Accrue(_config, TileRank.T2, false, 0, TimeSpan.FromMinutes(30));
            Assert.AreEqual(12, got);
        }

        [Test]
        public void Accrue_SubHourThatYieldsLessThanOneStone_AddsNothingYet()
        {
            // T1 = 10/hr. 5 minutes -> 0.833 Stone -> floored to 0.
            long got = TileYield.Accrue(_config, TileRank.T1, false, 0, TimeSpan.FromMinutes(5));
            Assert.AreEqual(0, got);
        }

        [Test]
        public void Accrue_AccumulatesOnTopOfExistingBalance()
        {
            // Start with 40 Stone on a T1 tile, add 3h * 10/hr = 30 -> 70.
            long got = TileYield.Accrue(_config, TileRank.T1, false, 40, TimeSpan.FromHours(3));
            Assert.AreEqual(70, got);
        }

        [Test]
        public void Accrue_NegativeSpan_NeverDestroysEarnedStone()
        {
            long got = TileYield.Accrue(_config, TileRank.T1, false, 55, TimeSpan.FromHours(-3));
            Assert.AreEqual(55, got);
        }

        // ── Keepfall Plus +50% (§6) ──────────────────────────────────────

        [TestCase(TileRank.T1, 15.0, 180)]
        [TestCase(TileRank.T2, 37.5, 450)]
        [TestCase(TileRank.T3, 90.0, 1080)]
        public void Plus_Scales_RateAndCap_ByFiftyPercent(
            TileRank rank, double expectedRate, long expectedCap)
        {
            Assert.AreEqual(expectedRate, TileYield.RatePerHour(_config, rank, true), 1e-9);
            Assert.AreEqual(expectedCap, TileYield.Cap(_config, rank, true));
        }

        [Test]
        public void Plus_StillFillsCapInTwelveHours()
        {
            // Fill time is unchanged (rate and cap both ×1.5); throughput per claim is +50%.
            long got = TileYield.Accrue(_config, TileRank.T1, true, 0, TimeSpan.FromHours(12));
            Assert.AreEqual(180, got);
        }

        [Test]
        public void Plus_PerClaimThroughput_IsFiftyPercentHigherThanFree()
        {
            long free = TileYield.Accrue(_config, TileRank.T2, false, 0, TimeSpan.FromHours(12));
            long plus = TileYield.Accrue(_config, TileRank.T2, true, 0, TimeSpan.FromHours(12));
            Assert.AreEqual(300, free);
            Assert.AreEqual(450, plus);
            Assert.AreEqual(free * 1.5, plus, 1e-9);
        }

        [Test]
        public void Cap_ShrinksWhenSubscriptionLapses_ClampingHeldStoneDown()
        {
            // A tile filled to the Plus cap (180) then loses Plus: it must clamp back to the
            // base cap (120) on the next accrual, not keep the bonus Stone indefinitely.
            long got = TileYield.Accrue(_config, TileRank.T1, false, 180, TimeSpan.FromHours(2));
            Assert.AreEqual(120, got);
        }
    }
}
