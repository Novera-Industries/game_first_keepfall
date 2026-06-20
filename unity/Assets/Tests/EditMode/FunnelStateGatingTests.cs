using System;
using Keepfall.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Funnel;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Proves the funnel gates on player STATE, not just <c>dayIndex</c> (source-of-truth §8:
    /// "PRECONDITION is player STATE, never wall-clock alone"). The headline case: reaching D3
    /// WITHOUT the first-unlock-outside-starters state does NOT fire <c>d3_starter_pack</c>;
    /// once the state is satisfied at the same day, it fires.
    /// </summary>
    public sealed class FunnelStateGatingTests
    {
        private static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

        private static (FunnelEngine engine, RecordingAnalytics analytics) Build()
        {
            var analytics = new RecordingAnalytics();
            var engine = new FunnelEngine(new RemoteConfig(), analytics);
            return (engine, analytics);
        }

        // ── D3 starter pack: day reached but STATE not satisfied ─────────

        [Test]
        public void D3StarterPack_DoesNotFire_WhenNoUnlockOutsideStarters()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            // Day 3 reached, Stone wall hit, but roster is still only the 6 starters.
            FunnelInputs inputs = StarterPackInputs(
                funnel, dayIndex: 3, rosterSize: 6, hasHitStoneWall: true);

            FunnelDecision d = engine.EvaluateTrigger(TriggerIds.D3StarterPack, inputs);

            Assert.IsFalse(d.Fired, "D3 alone must not fire the starter pack without the state.");
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, d.Reason);
        }

        [Test]
        public void D3StarterPack_DoesNotFire_WhenUnlockedButNoStoneWall()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            // Unlocked a 7th unit (outside starters) but has NOT hit a Stone wall.
            FunnelInputs inputs = StarterPackInputs(
                funnel, dayIndex: 3, rosterSize: 7, hasHitStoneWall: false);

            FunnelDecision d = engine.EvaluateTrigger(TriggerIds.D3StarterPack, inputs);

            Assert.IsFalse(d.Fired, "Both state legs (first unlock AND Stone wall) are required.");
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, d.Reason);
        }

        [Test]
        public void D3StarterPack_Fires_WhenDayAndBothStateLegsSatisfied()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            FunnelInputs inputs = StarterPackInputs(
                funnel, dayIndex: 3, rosterSize: 7, hasHitStoneWall: true);

            FunnelDecision d = engine.EvaluateTrigger(TriggerIds.D3StarterPack, inputs);

            Assert.IsTrue(d.Fired, "With the day AND both state legs, the banner fires.");
            Assert.AreEqual(FunnelPlacement.ShopTab, d.Presentation.Placement);
            Assert.IsTrue(analytics.Contains(Events.FunnelTriggerFired));
        }

        // ── The day can be "right" but every state-gated trigger stays shut ──

        [Test]
        public void Evaluate_AtD3_WithBareState_FiresNothing()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            // A fresh D3 player who has done nothing but reach the day: no waits, no unlocks,
            // no losses, no walls. The whole funnel must stay silent (no banner).
            FunnelInputs inputs = new FunnelInputs(
                dayIndex: 3, nowUtc: T0, tilesOwned: 3, rosterSize: 6, starterRosterSize: 6,
                stoneBalance: 0, hasHitStoneWall: false, ownsT3Tile: false, facesSpecialistWall: false,
                candidateAcceleratorTileId: null, hasWaitedOnTileYield: false, hasLostAdeptMatch: false,
                retryTokenCount: 0, currentLossMatchSeed: null, currentMatchLossStreak: 0,
                isExploringSynergies: false, unlockPacingSlowed: false, isPlusActive: false,
                isConverter: false, funnel: funnel);

            FunnelDecision d = engine.Evaluate(inputs);

            Assert.IsFalse(d.Fired, "No trigger may fire on day alone with bare state.");
            Assert.AreEqual(0, analytics.CountOf(Events.FunnelTriggerFired));
        }

        // ── D2 accelerator discover also needs the wait state ─────────────

        [Test]
        public void D2AcceleratorDiscover_NeedsWaitedOnYieldState()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            FunnelInputs notWaited = new FunnelInputs(
                dayIndex: 2, nowUtc: T0, tilesOwned: 3, rosterSize: 6, starterRosterSize: 6,
                stoneBalance: 0, hasHitStoneWall: false, ownsT3Tile: false, facesSpecialistWall: false,
                candidateAcceleratorTileId: null, hasWaitedOnTileYield: false, hasLostAdeptMatch: false,
                retryTokenCount: 0, currentLossMatchSeed: null, currentMatchLossStreak: 0,
                isExploringSynergies: false, unlockPacingSlowed: false, isPlusActive: false,
                isConverter: false, funnel: funnel);

            Assert.IsFalse(engine.EvaluateTrigger(TriggerIds.D2AcceleratorDiscover, notWaited).Fired,
                "D2 needs the 'waited on tile yield' state, not just the day.");

            FunnelInputs waited = new FunnelInputs(
                dayIndex: 2, nowUtc: T0, tilesOwned: 3, rosterSize: 6, starterRosterSize: 6,
                stoneBalance: 0, hasHitStoneWall: false, ownsT3Tile: false, facesSpecialistWall: false,
                candidateAcceleratorTileId: null, hasWaitedOnTileYield: true, hasLostAdeptMatch: false,
                retryTokenCount: 0, currentLossMatchSeed: null, currentMatchLossStreak: 0,
                isExploringSynergies: false, unlockPacingSlowed: false, isPlusActive: false,
                isConverter: false, funnel: funnel);

            Assert.IsTrue(engine.EvaluateTrigger(TriggerIds.D2AcceleratorDiscover, waited).Fired,
                "With the wait state at D2, the discovery icon surfaces.");
        }

        // ── The engine reads the canonical PlayerState save ───────────────

        [Test]
        public void FromPlayerState_ReadsRosterTilesAndSubscription()
        {
            var (engine, _) = Build();

            var state = new PlayerState();
            state.Funnel.DayIndex = 3;
            // 6 starters + 1 unlocked outside starters.
            for (int i = 0; i < 7; i++)
            {
                state.Roster.UnlockedUnitIds.Add("unit." + i);
            }

            state.Tiles.Add(new TileState("01", TileRank.T1, T0));
            state.Tiles.Add(new TileState("02", TileRank.T2, T0));
            state.Wallet.Stone = 40;

            // Build inputs straight from the save; assert the derived "outside starters" flag.
            FunnelInputs inputs = FunnelInputs.FromPlayerState(
                state, T0, starterRosterSize: 6, hasHitStoneWall: true);

            Assert.AreEqual(7, inputs.RosterSize);
            Assert.AreEqual(2, inputs.TilesOwned);
            Assert.IsTrue(inputs.HasUnlockedOutsideStarters);
            Assert.IsFalse(inputs.IsPlusActive);

            // And it fires the starter pack because the save-derived state satisfies both legs.
            FunnelDecision d = engine.EvaluateTrigger(TriggerIds.D3StarterPack, inputs);
            Assert.IsTrue(d.Fired);
        }

        [Test]
        public void FromPlayerState_PlusSubscriberIsConverter()
        {
            var state = new PlayerState();
            state.Funnel.DayIndex = 7;
            state.Subscription.Active = true;
            state.Subscription.ProductId = "keepfall.plus.monthly";

            FunnelInputs inputs = FunnelInputs.FromPlayerState(state, T0, unlockPacingSlowed: true);

            Assert.IsTrue(inputs.IsPlusActive);
            Assert.IsTrue(inputs.IsConverter, "A Plus subscriber is, by definition, a converter.");
        }

        private static FunnelInputs StarterPackInputs(
            FunnelState funnel, int dayIndex, int rosterSize, bool hasHitStoneWall)
        {
            return new FunnelInputs(
                dayIndex: dayIndex,
                nowUtc: T0,
                tilesOwned: 3,
                rosterSize: rosterSize,
                starterRosterSize: 6,
                stoneBalance: 40,
                hasHitStoneWall: hasHitStoneWall,
                ownsT3Tile: false,
                facesSpecialistWall: false,
                candidateAcceleratorTileId: null,
                hasWaitedOnTileYield: true,
                hasLostAdeptMatch: false,
                retryTokenCount: 0,
                currentLossMatchSeed: null,
                currentMatchLossStreak: 0,
                isExploringSynergies: false,
                unlockPacingSlowed: false,
                isPlusActive: false,
                isConverter: false,
                funnel: funnel);
        }
    }
}
