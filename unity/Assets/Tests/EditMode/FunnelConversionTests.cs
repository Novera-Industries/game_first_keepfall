using System;
using Keepfall.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Funnel;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// The conversion-model test (milestone 07, source-of-truth §8/§9). COMPLEMENTS the three
    /// existing funnel suites rather than duplicating them:
    /// <list type="bullet">
    ///   <item><see cref="FunnelStateGatingTests"/> proves STATE (not the day) gates a trigger.</item>
    ///   <item><see cref="FunnelFrequencyCapTests"/> locks the §8.2 frequency caps.</item>
    ///   <item><see cref="FunnelPostD30Tests"/> proves the converter/non-converter hard branch.</item>
    /// </list>
    /// This suite asserts the conversion model end to end: each key trigger FIRES under its
    /// precondition and is SUPPRESSED when the precondition is absent (the matched-pair the
    /// dashboard's fire→outcome join depends on); every fired presentation is a non-modal,
    /// dismissible banner (§10.5–§10.7); and a D31 non-converter yields <c>IsNone</c> via the
    /// post-D30 hard branch (§8.2). Key triggers covered: <c>d3_starter_pack</c>,
    /// <c>d7_plus_reveal_1</c>, <c>d11_accel_hint</c>, <c>d15_retry_nudge</c>, <c>d29_thanks</c>.
    /// </summary>
    public sealed class FunnelConversionTests
    {
        private static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

        private static (FunnelEngine engine, RecordingAnalytics analytics) Build()
        {
            var analytics = new RecordingAnalytics();
            var engine = new FunnelEngine(new RemoteConfig(), analytics); // canonical defaults
            return (engine, analytics);
        }

        // ── d3_starter_pack: precondition present → fires; absent → suppressed ──

        [Test]
        public void D3StarterPack_FiresUnderPrecondition_SuppressedWithout()
        {
            var (engine, analytics) = Build();

            // Precondition present: first unlock outside starters AND a Stone wall, at D3.
            FunnelDecision fired = engine.EvaluateTrigger(
                TriggerIds.D3StarterPack,
                ConversionInputs(new FunnelState(), dayIndex: 3, rosterSize: 7, hasHitStoneWall: true));
            Assert.IsTrue(fired.Fired, "d3_starter_pack fires when both state legs hold at D3.");
            Assert.AreEqual(FunnelPlacement.ShopTab, fired.Presentation.Placement);

            // Precondition absent: still only the 6 starters (no unlock outside).
            FunnelDecision suppressed = engine.EvaluateTrigger(
                TriggerIds.D3StarterPack,
                ConversionInputs(new FunnelState(), dayIndex: 3, rosterSize: 6, hasHitStoneWall: true));
            Assert.IsFalse(suppressed.Fired, "Without the unlock-outside-starters state it suppresses.");
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, suppressed.Reason);

            // The pair is bracketed by the dashboard events that close the loop.
            Assert.IsTrue(analytics.Contains(Events.FunnelTriggerFired));
            Assert.IsTrue(analytics.Contains(Events.FunnelTriggerSuppressed));
        }

        // ── d7_plus_reveal_1: pacing slowed → fires; not slowed → suppressed ──

        [Test]
        public void D7PlusReveal1_FiresWhenPacingSlowed_SuppressedWhenNot()
        {
            var (engine, _) = Build();

            FunnelInputs slowed = ConversionInputs(
                new FunnelState(), dayIndex: 7, rosterSize: 9, hasHitStoneWall: true,
                unlockPacingSlowed: true);
            FunnelDecision fired = engine.EvaluateTrigger(TriggerIds.D7PlusReveal1, slowed);
            Assert.IsTrue(fired.Fired, "Plus reveal #1 fires at D7 once pacing has slowed.");
            Assert.AreEqual(FunnelPlacement.ShopTab, fired.Presentation.Placement);

            FunnelInputs notSlowed = ConversionInputs(
                new FunnelState(), dayIndex: 7, rosterSize: 9, hasHitStoneWall: true,
                unlockPacingSlowed: false);
            FunnelDecision suppressed = engine.EvaluateTrigger(TriggerIds.D7PlusReveal1, notSlowed);
            Assert.IsFalse(suppressed.Fired, "Without the pacing-slowed state, no Plus reveal.");
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, suppressed.Reason);
        }

        // ── d11_accel_hint: T3 + specialist wall + candidate tile → fires; missing → suppressed ──

        [Test]
        public void D11AccelHint_FiresWithT3AndSpecialistWall_SuppressedWithout()
        {
            var (engine, _) = Build();

            FunnelInputs eligible = ConversionInputs(
                new FunnelState(), dayIndex: 11, rosterSize: 13, hasHitStoneWall: true,
                ownsT3Tile: true, facesSpecialistWall: true, candidateAcceleratorTileId: "T3-A");
            FunnelDecision fired = engine.EvaluateTrigger(TriggerIds.D11AccelHint, eligible);
            Assert.IsTrue(fired.Fired, "Accelerator hint fires near a T3 tile against a specialist wall.");
            Assert.AreEqual(FunnelPlacement.TileScreen, fired.Presentation.Placement);

            // No specialist wall → precondition unmet (the convenience the product solves is absent).
            FunnelInputs noWall = ConversionInputs(
                new FunnelState(), dayIndex: 11, rosterSize: 13, hasHitStoneWall: false,
                ownsT3Tile: true, facesSpecialistWall: false, candidateAcceleratorTileId: "T3-A");
            FunnelDecision suppressed = engine.EvaluateTrigger(TriggerIds.D11AccelHint, noWall);
            Assert.IsFalse(suppressed.Fired, "No specialist wall → the hint is not shown.");
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, suppressed.Reason);
        }

        // ── d15_retry_nudge: 3 same-match losses → fires; first loss → suppressed ──

        [Test]
        public void D15RetryNudge_FiresOnThirdLoss_SuppressedOnFirstLoss()
        {
            var (engine, _) = Build();

            FunnelInputs threeLosses = ConversionInputs(
                new FunnelState(), dayIndex: 16, rosterSize: 16, hasHitStoneWall: false,
                currentLossMatchSeed: "seed-1", currentMatchLossStreak: 3);
            FunnelDecision fired = engine.EvaluateTrigger(TriggerIds.D15RetryNudge, threeLosses);
            Assert.IsTrue(fired.Fired, "Retry nudge fires after 3 consecutive same-match losses.");
            Assert.AreEqual(FunnelPlacement.LossScreen, fired.Presentation.Placement);

            FunnelInputs firstLoss = ConversionInputs(
                new FunnelState(), dayIndex: 16, rosterSize: 16, hasHitStoneWall: false,
                currentLossMatchSeed: "seed-1", currentMatchLossStreak: 1);
            FunnelDecision suppressed = engine.EvaluateTrigger(TriggerIds.D15RetryNudge, firstLoss);
            Assert.IsFalse(suppressed.Fired, "Retry must never be offered on the first loss (§8.2).");
            Assert.AreEqual(SuppressionReason.NotFirstLossRule, suppressed.Reason);
        }

        // ── d29_thanks: month-end → fires (no sell); off-day → suppressed ──

        [Test]
        public void D29Thanks_FiresAtMonthEnd_SuppressedBeforeIt()
        {
            var (engine, _) = Build();

            FunnelDecision fired = engine.EvaluateTrigger(
                TriggerIds.D29Thanks,
                ConversionInputs(new FunnelState(), dayIndex: 29, rosterSize: 21, hasHitStoneWall: false));
            Assert.IsTrue(fired.Fired, "The month-end thanks beat fires at D29.");
            Assert.AreEqual(FunnelPlacement.Profile, fired.Presentation.Placement);

            FunnelDecision suppressed = engine.EvaluateTrigger(
                TriggerIds.D29Thanks,
                ConversionInputs(new FunnelState(), dayIndex: 20, rosterSize: 21, hasHitStoneWall: false));
            Assert.IsFalse(suppressed.Fired, "Before the month-end window the thanks beat is not eligible.");
            Assert.AreEqual(SuppressionReason.PreconditionUnmet, suppressed.Reason);
        }

        // ── Presentation contract: never a modal on open, always dismissible ──

        [Test]
        public void EveryFiredPresentation_IsNonModalAndDismissible()
        {
            var (engine, _) = Build();

            // One fired presentation from each surface (Shop, tile, loss, pass, profile).
            FunnelPresentation[] fired =
            {
                FireOrFail(engine, TriggerIds.D3StarterPack,
                    ConversionInputs(new FunnelState(), 3, 7, hasHitStoneWall: true)),
                FireOrFail(engine, TriggerIds.D11AccelHint,
                    ConversionInputs(new FunnelState(), 11, 13, hasHitStoneWall: true,
                        ownsT3Tile: true, facesSpecialistWall: true, candidateAcceleratorTileId: "T3-A")),
                FireOrFail(engine, TriggerIds.D15RetryNudge,
                    ConversionInputs(new FunnelState(), 16, 16, hasHitStoneWall: false,
                        currentLossMatchSeed: "seed-1", currentMatchLossStreak: 3)),
                FireOrFail(engine, TriggerIds.D8Battlepass1,
                    ConversionInputs(new FunnelState(), 8, 11, hasHitStoneWall: false,
                        isExploringSynergies: true)),
                FireOrFail(engine, TriggerIds.D29Thanks,
                    ConversionInputs(new FunnelState(), 29, 21, hasHitStoneWall: false)),
            };

            foreach (FunnelPresentation p in fired)
            {
                Assert.IsFalse(p.IsModal, $"{p.TriggerId} must never be a modal (§10.5/§10.6).");
                Assert.IsTrue(p.IsDismissible, $"{p.TriggerId} must always be dismissible (§8).");
                Assert.AreNotEqual(string.Empty, p.BodyCopy, $"{p.TriggerId} must carry calm copy.");
                // The placement enum is a closed set with no app-open value, so a fired banner can
                // physically never target app open (§10.5/§10.6 encoded in FunnelPlacement).
                Assert.IsTrue(Enum.IsDefined(typeof(FunnelPlacement), p.Placement));
            }
        }

        // ── D31 non-converter: IsNone via the post-D30 hard branch (§8.2) ──

        [Test]
        public void D31NonConverter_YieldsIsNone_ViaPostD30HardBranch()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            // Otherwise-eligible state for several triggers, but a non-converter past the cliff.
            FunnelInputs inputs = ConversionInputs(
                funnel, dayIndex: 31, rosterSize: 20, hasHitStoneWall: true,
                ownsT3Tile: true, facesSpecialistWall: true, candidateAcceleratorTileId: "T3-A",
                unlockPacingSlowed: true, currentLossMatchSeed: "seed-late",
                currentMatchLossStreak: 3, isExploringSynergies: true);

            FunnelDecision decision = engine.Evaluate(inputs);

            Assert.IsTrue(decision.IsNone, "A D31 non-converter receives no new trigger (None).");
            Assert.IsFalse(decision.Fired);
            Assert.AreEqual(0, analytics.CountOf(Events.FunnelTriggerFired),
                "No funnel trigger may fire for a post-D30 non-converter.");
            Assert.AreEqual(1, analytics.CountOf(Events.FunnelPostD30Suppressed),
                "The hard branch emits funnel_postd30_suppressed once at the boundary.");
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static FunnelPresentation FireOrFail(
            FunnelEngine engine, string triggerId, FunnelInputs inputs)
        {
            FunnelDecision d = engine.EvaluateTrigger(triggerId, inputs);
            Assert.IsTrue(d.Fired, $"{triggerId} was expected to fire under its precondition.");
            return d.Presentation;
        }

        // One builder shared across the suite (mirrors the other funnel suites' idiom of a single
        // full-constructor helper with named optionals for the legs each test flips).
        private static FunnelInputs ConversionInputs(
            FunnelState funnel,
            int dayIndex,
            int rosterSize,
            bool hasHitStoneWall,
            bool ownsT3Tile = false,
            bool facesSpecialistWall = false,
            string candidateAcceleratorTileId = null,
            bool unlockPacingSlowed = false,
            string currentLossMatchSeed = null,
            int currentMatchLossStreak = 0,
            bool isExploringSynergies = false)
        {
            return new FunnelInputs(
                dayIndex: dayIndex,
                nowUtc: T0,
                tilesOwned: 5,
                rosterSize: rosterSize,
                starterRosterSize: 6,
                stoneBalance: hasHitStoneWall ? 40 : 1000,
                hasHitStoneWall: hasHitStoneWall,
                ownsT3Tile: ownsT3Tile,
                facesSpecialistWall: facesSpecialistWall,
                candidateAcceleratorTileId: candidateAcceleratorTileId,
                hasWaitedOnTileYield: true,
                hasLostAdeptMatch: currentLossMatchSeed != null,
                retryTokenCount: currentLossMatchSeed != null ? 0 : 1,
                currentLossMatchSeed: currentLossMatchSeed,
                currentMatchLossStreak: currentMatchLossStreak,
                isExploringSynergies: isExploringSynergies,
                unlockPacingSlowed: unlockPacingSlowed,
                isPlusActive: false,
                isConverter: false,
                funnel: funnel);
        }
    }
}
