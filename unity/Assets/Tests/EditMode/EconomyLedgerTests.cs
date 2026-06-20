using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Data;
using Keepfall.Economy;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Unit-unlock checks for <see cref="EconomyLedger"/> (source-of-truth §2 cost ladder,
    /// §3 roster, §10.2 "no unit is ever gated by money"). Asserts: unlocking spends Stone and
    /// records ownership; an off-ladder or unaffordable price is refused with no side effects;
    /// and there is no Shard path to a unit at all — Shards spent elsewhere never unlock a unit.
    /// </summary>
    public sealed class EconomyLedgerTests
    {
        private (EconomyLedger ledger, Wallet wallet, RosterState roster, RecordingAnalytics fx)
            NewLedger(long stone, long shards = 0)
        {
            var walletState = new WalletState(stone, shards);
            var wallet = new Wallet(walletState);
            var roster = new RosterState();
            var fx = new RecordingAnalytics();
            var ledger = new EconomyLedger(wallet, roster, fx);
            return (ledger, wallet, roster, fx);
        }

        // ── Successful unlock spends Stone ───────────────────────────────

        [Test]
        public void UnlockUnit_SpendsStone_AndRecordsOwnershipAndPrice()
        {
            (EconomyLedger ledger, Wallet wallet, RosterState roster, RecordingAnalytics fx) =
                NewLedger(stone: 500);

            UnlockResult result = ledger.UnlockUnit("core.longshot", UnlockTier.Core, 300);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(300, result.StoneSpent);
            Assert.AreEqual(200, wallet.GetBalance(CurrencyType.Stone), "Stone debited.");
            CollectionAssert.Contains(roster.UnlockedUnitIds, "core.longshot");
            Assert.AreEqual(300, roster.StoneSpentLedger["core.longshot"], "Price recorded.");
            Assert.AreEqual(1, fx.CountOf(Events.UnitUnlocked));
        }

        [Test]
        public void UnlockUnit_AnalyticsAlwaysRecordsStoneCurrency()
        {
            (EconomyLedger ledger, _, _, RecordingAnalytics fx) = NewLedger(stone: 3000);

            ledger.UnlockUnit("spec.wildfire", UnlockTier.Specialist, 2500);

            RecordingAnalytics.Entry e = fx.Events[0];
            Assert.AreEqual(Events.UnitUnlocked, e.Event);
            Assert.AreEqual(
                CurrencyType.Stone.ToString(), e.Props["currency"],
                "A unit unlock is always recorded as a Stone purchase, never Shards.");
        }

        [TestCase(UnlockTier.Starter, 100)]
        [TestCase(UnlockTier.Core, 1200)]
        [TestCase(UnlockTier.Specialist, 6000)]
        [TestCase(UnlockTier.Master, 15000)]
        public void UnlockUnit_AcceptsCostsWithinCanonicalBand(UnlockTier tier, long cost)
        {
            (EconomyLedger ledger, _, RosterState roster, _) = NewLedger(stone: 20000);

            UnlockResult result = ledger.UnlockUnit($"unit.{tier}", tier, cost);

            Assert.IsTrue(result.Success);
            CollectionAssert.Contains(roster.UnlockedUnitIds, $"unit.{tier}");
        }

        // ── Refuses when poor ────────────────────────────────────────────

        [Test]
        public void UnlockUnit_WhenInsufficientStone_RefusesAndChangesNothing()
        {
            (EconomyLedger ledger, Wallet wallet, RosterState roster, RecordingAnalytics fx) =
                NewLedger(stone: 100);

            UnlockResult result = ledger.UnlockUnit("core.longshot", UnlockTier.Core, 300);

            Assert.IsFalse(result.Success, "Cannot afford 300 with 100 Stone.");
            Assert.AreEqual(0, result.StoneSpent);
            Assert.AreEqual(100, wallet.GetBalance(CurrencyType.Stone), "No Stone debited.");
            CollectionAssert.IsEmpty(roster.UnlockedUnitIds, "No unit recorded.");
            Assert.AreEqual(0, fx.CountOf(Events.UnitUnlocked), "No unlock event on refusal.");
        }

        // ── Off-ladder prices are refused (costs trace to §2) ────────────

        [TestCase(UnlockTier.Core, 299)]     // just below band
        [TestCase(UnlockTier.Core, 1201)]    // just above band
        [TestCase(UnlockTier.Specialist, 100)]
        [TestCase(UnlockTier.Master, 9999)]
        public void UnlockUnit_OffLadderCost_IsRefused(UnlockTier tier, long cost)
        {
            (EconomyLedger ledger, Wallet wallet, RosterState roster, _) =
                NewLedger(stone: 50000);

            UnlockResult result = ledger.UnlockUnit("unit.x", tier, cost);

            Assert.IsFalse(result.Success, $"{cost} is outside the {tier} band.");
            Assert.AreEqual(50000, wallet.GetBalance(CurrencyType.Stone), "Wallet untouched.");
            CollectionAssert.IsEmpty(roster.UnlockedUnitIds);
        }

        // ── No Shard path to a unit (the §10.2 invariant) ────────────────

        [Test]
        public void NoApiAcceptsShardsForAUnit_ShardsAreNeverSpentOnUnlock()
        {
            // Wallet has zero Stone but a large Shard balance. A unit must remain unobtainable:
            // there is no overload that could route Shards to a unit, so the unlock fails on
            // Stone affordability and the Shard balance is left completely untouched.
            (EconomyLedger ledger, Wallet wallet, RosterState roster, _) =
                NewLedger(stone: 0, shards: 9999);

            UnlockResult result = ledger.UnlockUnit("core.longshot", UnlockTier.Core, 300);

            Assert.IsFalse(result.Success, "A unit can never be bought with money (§10.2).");
            Assert.AreEqual(9999, wallet.GetBalance(CurrencyType.Shards),
                "Shards must not be consumed by a unit unlock under any circumstance.");
            Assert.AreEqual(0, wallet.GetBalance(CurrencyType.Stone));
            CollectionAssert.IsEmpty(roster.UnlockedUnitIds);
        }

        // ── Free starters and idempotency ────────────────────────────────

        [Test]
        public void GrantStarterUnit_IsFree_AndRecordsZeroCost()
        {
            (EconomyLedger ledger, Wallet wallet, RosterState roster, _) = NewLedger(stone: 0);

            UnlockResult result = ledger.GrantStarterUnit("starter.bulwark");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.StoneSpent);
            Assert.AreEqual(0, wallet.GetBalance(CurrencyType.Stone), "Starters are free.");
            Assert.AreEqual(0, roster.StoneSpentLedger["starter.bulwark"]);
        }

        [Test]
        public void UnlockUnit_AlreadyOwned_IsNoOp_NoDoubleSpend()
        {
            (EconomyLedger ledger, Wallet wallet, RosterState roster, _) = NewLedger(stone: 1000);

            ledger.UnlockUnit("core.longshot", UnlockTier.Core, 300);
            UnlockResult second = ledger.UnlockUnit("core.longshot", UnlockTier.Core, 300);

            Assert.IsTrue(second.Success, "Already-owned returns a benign success.");
            Assert.AreEqual(0, second.StoneSpent, "No second debit.");
            Assert.AreEqual(700, wallet.GetBalance(CurrencyType.Stone), "Charged exactly once.");
            Assert.AreEqual(1, roster.UnlockedUnitIds.Count, "Owned exactly once.");
        }

        // ── Cost-band exposure (for tuning validators) ───────────────────

        [Test]
        public void CostBandFor_MatchesCanonicalLadder()
        {
            Assert.AreEqual((0L, 150L), EconomyLedger.CostBandFor(UnlockTier.Starter));
            Assert.AreEqual((300L, 1200L), EconomyLedger.CostBandFor(UnlockTier.Core));
            Assert.AreEqual((2500L, 6000L), EconomyLedger.CostBandFor(UnlockTier.Specialist));
            Assert.AreEqual((10000L, 15000L), EconomyLedger.CostBandFor(UnlockTier.Master));
        }
    }
}
