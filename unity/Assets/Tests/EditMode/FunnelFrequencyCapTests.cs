using System;
using Keepfall.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Funnel;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Locks in the funnel frequency caps from source-of-truth §8.2 (taxonomy §6):
    /// <list type="bullet">
    ///   <item>Keepfall Plus reveal capped at 3 in 30 days.</item>
    ///   <item>Accelerator hint: max 1 per tile per week, and a 7-day suppression after an
    ///   accelerator is USED.</item>
    ///   <item>Retry offer requires 3 consecutive same-match losses and is suppressed on the
    ///   first loss.</item>
    /// </list>
    /// </summary>
    public sealed class FunnelFrequencyCapTests
    {
        private static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        private static (FunnelEngine engine, RecordingAnalytics analytics) Build()
        {
            var analytics = new RecordingAnalytics();
            var engine = new FunnelEngine(new RemoteConfig(), analytics); // canonical defaults
            return (engine, analytics);
        }

        // Inputs for a non-converter who is eligible for a Plus reveal (pacing slowed, not Plus).
        private static FunnelInputs PlusRevealInputs(
            FunnelState funnel, int dayIndex, DateTimeOffset now)
        {
            return new FunnelInputs(
                dayIndex: dayIndex,
                nowUtc: now,
                tilesOwned: 5,
                rosterSize: 9,
                starterRosterSize: 6,
                stoneBalance: 100,
                hasHitStoneWall: true,
                ownsT3Tile: false,
                facesSpecialistWall: false,
                candidateAcceleratorTileId: null,
                hasWaitedOnTileYield: true,
                hasLostAdeptMatch: false,
                retryTokenCount: 0,
                currentLossMatchSeed: null,
                currentMatchLossStreak: 0,
                isExploringSynergies: false,
                unlockPacingSlowed: true,
                isPlusActive: false,
                isConverter: false,
                funnel: funnel);
        }

        // ── Plus reveal: capped at 3 / 30 days ───────────────────────────

        [Test]
        public void PlusReveal_FiresThreeTimesAcrossThirtyDays_ThenIsCapped()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            // Reveal #1 at D7 (Shop tab).
            FunnelDecision r1 = engine.EvaluateTrigger(
                TriggerIds.D7PlusReveal1, PlusRevealInputs(funnel, 7, T0));
            Assert.IsTrue(r1.Fired, "Reveal #1 should fire at D7 with pacing slowed.");
            Assert.AreEqual(FunnelPlacement.ShopTab, r1.Presentation.Placement);
            Assert.AreEqual(1, funnel.PlusRevealCount);

            // Reveal #2 at D14 (Profile) — reveal #1 happened and did not convert.
            FunnelDecision r2 = engine.EvaluateTrigger(
                TriggerIds.D14PlusReveal2, PlusRevealInputs(funnel, 14, T0.AddDays(7)));
            Assert.IsTrue(r2.Fired, "Reveal #2 should fire at D14 after a non-converting #1.");
            Assert.AreEqual(FunnelPlacement.Profile, r2.Presentation.Placement);
            Assert.AreEqual(2, funnel.PlusRevealCount);

            // Reveal #3 at D22 (Profile) — #1 and #2 both shown, still not Plus.
            FunnelDecision r3 = engine.EvaluateTrigger(
                TriggerIds.D22PlusReveal3, PlusRevealInputs(funnel, 22, T0.AddDays(15)));
            Assert.IsTrue(r3.Fired, "Reveal #3 (final) should fire at D22.");
            Assert.AreEqual(3, funnel.PlusRevealCount);

            // Three reveals fired in the 30-day window. The engine emits only the bracketing
            // funnel_trigger_fired (the plus_reveal_shown surface event is the monetization
            // layer's job, downstream of this decision), so we count those by trigger id.
            int plusFires = 0;
            foreach (RecordingAnalytics.Entry e in analytics.Events)
            {
                if (e.Event != Events.FunnelTriggerFired || e.Props == null)
                {
                    continue;
                }

                if (e.Props.TryGetValue("trigger_id", out object tid)
                    && (Equals(tid, TriggerIds.D7PlusReveal1)
                        || Equals(tid, TriggerIds.D14PlusReveal2)
                        || Equals(tid, TriggerIds.D22PlusReveal3)))
                {
                    plusFires++;
                }
            }

            Assert.AreEqual(3, plusFires, "Exactly three plus reveals should have fired.");

            // A fourth attempt inside the same 30-day window is capped (still day 22 window, but
            // try re-firing #3 within 30 days): max 3 / 30 days (SoT §8.2).
            FunnelDecision r4 = engine.EvaluateTrigger(
                TriggerIds.D22PlusReveal3, PlusRevealInputs(funnel, 24, T0.AddDays(17)));
            Assert.IsFalse(r4.Fired, "A 4th reveal inside 30 days must be capped.");
            Assert.AreEqual(SuppressionReason.FreqCapHit, r4.Reason);
            Assert.AreEqual(3, funnel.PlusRevealCount, "Capped reveal must not bump the counter.");
        }

        [Test]
        public void PlusReveal_NotShownToExistingSubscriber()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();
            FunnelInputs inputs = PlusRevealInputs(funnel, 7, T0);
            // Make the player a Plus subscriber.
            inputs = WithPlus(inputs);

            FunnelDecision d = engine.EvaluateTrigger(TriggerIds.D7PlusReveal1, inputs);

            Assert.IsFalse(d.Fired);
            Assert.AreEqual(SuppressionReason.AlreadyConverted, d.Reason);
        }

        // ── Accelerator hint: 1 / tile / week + 7-day suppression after use ──

        [Test]
        public void AcceleratorHint_OncePerTilePerWeek()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            FunnelInputs First = AccelHintInputs(funnel, 11, T0, tileId: "T3-A");
            FunnelDecision d1 = engine.EvaluateTrigger(TriggerIds.D11AccelHint, First);
            Assert.IsTrue(d1.Fired, "First hint for the tile should fire.");
            Assert.AreEqual(FunnelPlacement.TileScreen, d1.Presentation.Placement);

            // Same tile, 3 days later — inside the 7-day per-tile window.
            FunnelDecision d2 = engine.EvaluateTrigger(
                TriggerIds.D11AccelHint, AccelHintInputs(funnel, 14, T0.AddDays(3), tileId: "T3-A"));
            Assert.IsFalse(d2.Fired, "A second hint within a week for the same tile is capped.");
            Assert.AreEqual(SuppressionReason.FreqCapHit, d2.Reason);
        }

        [Test]
        public void AcceleratorHint_SuppressedForSevenDaysAfterAcceleratorUsed()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            // Player USED an accelerator on day 11.
            FunnelEngine.NoteAcceleratorUsed(funnel, T0);

            // 3 days later, a hint for a DIFFERENT tile must still be suppressed (never to a
            // player who used an accelerator in the past 7 days — SoT §8.2).
            FunnelDecision d = engine.EvaluateTrigger(
                TriggerIds.D11AccelHint, AccelHintInputs(funnel, 14, T0.AddDays(3), tileId: "T3-B"));
            Assert.IsFalse(d.Fired);
            Assert.AreEqual(SuppressionReason.RecentlyUsedAccelerator, d.Reason);

            // 8 days after the use, the suppression has lifted.
            FunnelDecision d2 = engine.EvaluateTrigger(
                TriggerIds.D11AccelHint, AccelHintInputs(funnel, 14, T0.AddDays(8), tileId: "T3-B"));
            Assert.IsTrue(d2.Fired, "After 7 days the recent-use suppression lifts.");
        }

        // ── Retry offer: 3 consecutive same-match losses, never first loss ──

        [Test]
        public void RetryOffer_SuppressedOnFirstLoss()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            FunnelDecision d = engine.EvaluateTrigger(
                TriggerIds.D15RetryNudge, RetryInputs(funnel, 16, "seed-1", lossStreak: 1));

            Assert.IsFalse(d.Fired, "Retry must never be offered on the first loss.");
            Assert.AreEqual(SuppressionReason.NotFirstLossRule, d.Reason);
        }

        [Test]
        public void RetryOffer_SuppressedOnSecondLoss()
        {
            var (engine, _) = Build();
            var funnel = new FunnelState();

            FunnelDecision d = engine.EvaluateTrigger(
                TriggerIds.D15RetryNudge, RetryInputs(funnel, 16, "seed-1", lossStreak: 2));

            Assert.IsFalse(d.Fired, "Two losses is still below the 3-loss gate.");
            Assert.AreEqual(SuppressionReason.NotFirstLossRule, d.Reason);
        }

        [Test]
        public void RetryOffer_FiresOnThirdConsecutiveSameMatchLoss()
        {
            var (engine, analytics) = Build();
            var funnel = new FunnelState();

            FunnelDecision d = engine.EvaluateTrigger(
                TriggerIds.D15RetryNudge, RetryInputs(funnel, 16, "seed-1", lossStreak: 3));

            Assert.IsTrue(d.Fired, "Retry fires after 3 consecutive same-match losses.");
            Assert.AreEqual(FunnelPlacement.LossScreen, d.Presentation.Placement);
            Assert.IsTrue(analytics.Contains(Events.FunnelTriggerFired));
        }

        // ── Builders ──────────────────────────────────────────────────────

        private static FunnelInputs AccelHintInputs(
            FunnelState funnel, int dayIndex, DateTimeOffset now, string tileId)
        {
            return new FunnelInputs(
                dayIndex: dayIndex,
                nowUtc: now,
                tilesOwned: 8,
                rosterSize: 12,
                starterRosterSize: 6,
                stoneBalance: 500,
                hasHitStoneWall: true,
                ownsT3Tile: true,
                facesSpecialistWall: true,
                candidateAcceleratorTileId: tileId,
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

        private static FunnelInputs RetryInputs(
            FunnelState funnel, int dayIndex, string seed, int lossStreak)
        {
            return new FunnelInputs(
                dayIndex: dayIndex,
                nowUtc: T0,
                tilesOwned: 10,
                rosterSize: 16,
                starterRosterSize: 6,
                stoneBalance: 1000,
                hasHitStoneWall: false,
                ownsT3Tile: true,
                facesSpecialistWall: false,
                candidateAcceleratorTileId: null,
                hasWaitedOnTileYield: true,
                hasLostAdeptMatch: true,
                retryTokenCount: 0,
                currentLossMatchSeed: seed,
                currentMatchLossStreak: lossStreak,
                isExploringSynergies: true,
                unlockPacingSlowed: false,
                isPlusActive: false,
                isConverter: false,
                funnel: funnel);
        }

        private static FunnelInputs WithPlus(FunnelInputs i)
        {
            return new FunnelInputs(
                i.DayIndex, i.NowUtc, i.TilesOwned, i.RosterSize, i.StarterRosterSize,
                i.StoneBalance, i.HasHitStoneWall, i.OwnsT3Tile, i.FacesSpecialistWall,
                i.CandidateAcceleratorTileId, i.HasWaitedOnTileYield, i.HasLostAdeptMatch,
                i.RetryTokenCount, i.CurrentLossMatchSeed, i.CurrentMatchLossStreak,
                i.IsExploringSynergies, i.UnlockPacingSlowed,
                isPlusActive: true, isConverter: true, funnel: i.Funnel);
        }
    }
}
