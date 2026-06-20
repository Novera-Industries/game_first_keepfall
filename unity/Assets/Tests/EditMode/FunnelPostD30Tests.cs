using System;
using Keepfall.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Funnel;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// THE REQUIRED HARD-BRANCH TEST (source-of-truth §8.2, taxonomy §5):
    /// after D30 a NON-converter (no Plus, no meaningful spend) receives NO new triggers — the
    /// engine returns <see cref="FunnelDecision.None"/> and emits
    /// <see cref="Events.FunnelPostD30Suppressed"/> once at the boundary. A CONVERTER's path
    /// differs: they are not in the hard branch and continue through the registry (where Plus is
    /// already handled as "already converted").
    /// </summary>
    public sealed class FunnelPostD30Tests
    {
        private static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        private static (FunnelEngine engine, RecordingAnalytics analytics) Build()
        {
            var analytics = new RecordingAnalytics();
            var engine = new FunnelEngine(new RemoteConfig(), analytics);
            return (engine, analytics);
        }

        // A D31 player with otherwise-eligible state for several triggers, parameterised on
        // converter-ness. If the hard branch were an emergent side-effect rather than an explicit
        // gate, a converter and non-converter would behave the same here — they must not.
        private static FunnelInputs D31Inputs(FunnelState funnel, bool isConverter, bool isPlus)
        {
            return new FunnelInputs(
                dayIndex: 31,
                nowUtc: T0,
                tilesOwned: 11,
                rosterSize: 20,
                starterRosterSize: 6,
                stoneBalance: 50,
                hasHitStoneWall: true,
                ownsT3Tile: true,
                facesSpecialistWall: true,
                candidateAcceleratorTileId: "T3-A",
                hasWaitedOnTileYield: true,
                hasLostAdeptMatch: true,
                retryTokenCount: 0,
                currentLossMatchSeed: "seed-late",
                currentMatchLossStreak: 3,
                isExploringSynergies: true,
                unlockPacingSlowed: true,
                isPlusActive: isPlus,
                isConverter: isConverter,
                funnel: funnel);
        }

        // ── Non-converter: hard branch off ────────────────────────────────

        [Test]
        public void D31NonConverter_GetsNoNewTriggers_AndEmitsPostD30SuppressedOnce()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            FunnelDecision decision = engine.Evaluate(D31Inputs(funnel, isConverter: false, isPlus: false));

            Assert.IsTrue(decision.IsNone, "A D31 non-converter must receive no new trigger.");
            Assert.IsFalse(decision.Fired);
            Assert.AreEqual(1, analytics.CountOf(Events.FunnelPostD30Suppressed),
                "The post-D30 suppression event must be emitted exactly once at the boundary.");

            // No trigger fired at all.
            Assert.AreEqual(0, analytics.CountOf(Events.FunnelTriggerFired),
                "No funnel trigger may fire for a post-D30 non-converter.");

            // The boundary event carries is_converter:false and a positive suppressed count.
            RecordingAnalytics.Entry evt = LastOf(analytics, Events.FunnelPostD30Suppressed);
            Assert.AreEqual(false, evt.Props["is_converter"]);
            Assert.AreEqual(31, evt.Props["day_index"]);
            Assert.Greater((int)evt.Props["triggers_now_suppressed"], 0);
        }

        [Test]
        public void D31NonConverter_PostD30EventEmittedOnlyOnce_AcrossMultiplePasses()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            engine.Evaluate(D31Inputs(funnel, isConverter: false, isPlus: false));
            engine.Evaluate(D31Inputs(funnel, isConverter: false, isPlus: false));
            engine.Evaluate(D31Inputs(funnel, isConverter: false, isPlus: false));

            Assert.AreEqual(1, analytics.CountOf(Events.FunnelPostD30Suppressed),
                "The boundary event is once-per-player, persisted in FunnelState across passes.");
        }

        [Test]
        public void D31NonConverter_IndividualTriggerEvaluation_IsSuppressedWithPostD30Reason()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            // Even asking for a single trigger directly is gated by the hard branch.
            FunnelDecision d = engine.EvaluateTrigger(
                TriggerIds.D22PlusReveal3, D31Inputs(funnel, isConverter: false, isPlus: false));

            Assert.IsFalse(d.Fired);
            Assert.AreEqual(SuppressionReason.PostD30, d.Reason);
        }

        // ── Converter: path differs (NOT in the hard branch) ──────────────

        [Test]
        public void D31Converter_IsNotInHardBranch_NoPostD30Event()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            // A Plus subscriber is a converter. The hard branch does NOT apply to them.
            FunnelDecision decision = engine.Evaluate(D31Inputs(funnel, isConverter: true, isPlus: true));

            Assert.AreEqual(0, analytics.CountOf(Events.FunnelPostD30Suppressed),
                "A converter is never in the post-D30 non-converter branch.");
            Assert.IsFalse(decision.Fired,
                "A D31 converter has no new beat either, but for a DIFFERENT reason than the "
                + "non-converter hard stop (see the per-trigger reason test below).");
        }

        [Test]
        public void D31_PerTrigger_DiffersByConverter_NormalGateVsPostD30()
        {
            var (engine, _) = Build();

            // SAME day (D31), SAME trigger — the ONLY difference is converter-ness. A converter is
            // NOT in the hard branch, so the trigger falls through to its normal day-window check
            // (the D7 reveal beat is long closed by D31 → precondition_unmet). A non-converter is
            // hard-stopped by the post-D30 branch BEFORE any per-trigger logic runs. Different
            // reasons prove the branch is an explicit, converter-aware gate, not a coincidence.
            FunnelDecision converter = engine.EvaluateTrigger(
                TriggerIds.D7PlusReveal1, D31Inputs(new FunnelState(), isConverter: true, isPlus: true));
            Assert.IsFalse(converter.Fired);
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, converter.Reason,
                "A converter falls through to the normal (closed) day-window gate.");

            FunnelDecision nonConverter = engine.EvaluateTrigger(
                TriggerIds.D7PlusReveal1, D31Inputs(new FunnelState(), isConverter: false, isPlus: false));
            Assert.IsFalse(nonConverter.Fired);
            Assert.AreEqual(SuppressionReason.PostD30, nonConverter.Reason,
                "A non-converter is gated by the post-D30 hard branch before per-trigger logic.");
        }

        [Test]
        public void HardBranch_IsExplicit_TryPostD30HardBranchReturnsTrueOnlyForNonConverter()
        {
            var (engine, _) = Build();

            // Explicit branch API: true (handled) for a D31 non-converter, false for a converter.
            Assert.IsTrue(
                engine.TryPostD30HardBranch(
                    D31Inputs(new FunnelState(), isConverter: false, isPlus: false), out _),
                "Non-converter at D31 is handled by the explicit hard branch.");

            Assert.IsFalse(
                engine.TryPostD30HardBranch(
                    D31Inputs(new FunnelState(), isConverter: true, isPlus: true), out _),
                "Converter at D31 is NOT handled by the hard branch.");
        }

        [Test]
        public void PreD30NonConverter_IsNotInHardBranch()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            // D29: still pre-cliff. The hard branch must not fire; the thanks trigger is eligible.
            FunnelInputs i = new FunnelInputs(
                dayIndex: 29, nowUtc: T0, tilesOwned: 11, rosterSize: 20, starterRosterSize: 6,
                stoneBalance: 50, hasHitStoneWall: true, ownsT3Tile: true, facesSpecialistWall: true,
                candidateAcceleratorTileId: "T3-A", hasWaitedOnTileYield: true, hasLostAdeptMatch: true,
                retryTokenCount: 0, currentLossMatchSeed: "seed", currentMatchLossStreak: 0,
                isExploringSynergies: true, unlockPacingSlowed: true, isPlusActive: false,
                isConverter: false, funnel: funnel);

            FunnelDecision d = engine.Evaluate(i);

            Assert.AreEqual(0, analytics.CountOf(Events.FunnelPostD30Suppressed),
                "No post-D30 suppression before the D30 cliff.");
            Assert.IsTrue(d.Fired, "D29 thanks should fire for the still-active player.");
            Assert.AreEqual(TriggerIds.D29Thanks, d.Presentation.TriggerId);
        }

        private static RecordingAnalytics.Entry LastOf(RecordingAnalytics a, string name)
        {
            RecordingAnalytics.Entry found = default;
            foreach (RecordingAnalytics.Entry e in a.Events)
            {
                if (e.Event == name)
                {
                    found = e;
                }
            }

            return found;
        }
    }
}
