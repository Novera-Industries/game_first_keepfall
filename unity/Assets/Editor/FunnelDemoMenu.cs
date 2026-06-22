using System;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Funnel;
using UnityEditor;
using UnityEngine;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// Milestone 07 on-ramp (<c>milestone/07-funnel-analytics</c>: "conversion model
    /// measurable", source-of-truth §8/§9/§13). Walks the 30-day conversion funnel against a
    /// real <see cref="FunnelEngine"/> and logs, day by day, which trigger fires (id + placement)
    /// or that none did / why it was suppressed — so the funnel is observable headlessly without a
    /// device or a live GameAnalytics/Firebase sink.
    ///
    /// <para>The engine is the canonical one (<c>new RemoteConfig()</c> defaults +
    /// <see cref="DebugAnalytics"/> + a fresh <see cref="FunnelState"/>); each day supplies
    /// representative <see cref="FunnelInputs"/> for that day's STATE per the §8 table, using the
    /// exact constructor. A second item demonstrates the post-D30 non-converter hard branch.</para>
    ///
    /// <para>Menu: <b>Keepfall ▸ Funnel</b>. Decision logic:
    /// <see cref="FunnelEngine.Evaluate"/>; copy/tone obeys §12 — calm, no exclamation points.</para>
    /// </summary>
    public static class FunnelDemoMenu
    {
        // A fixed anchor so the log is deterministic run to run. The funnel reads relative time
        // (caps), so the absolute date only needs to be stable, not "now".
        private static readonly DateTimeOffset Anchor =
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        [MenuItem("Keepfall/Funnel/Simulate 30-Day Funnel")]
        public static void Simulate30DayFunnel()
        {
            var config = new RemoteConfig();
            var analytics = new DebugAnalytics();
            var engine = new FunnelEngine(config, analytics);

            Debug.Log(
                "[Funnel] Simulating the 30-day conversion funnel (source-of-truth §8). "
                + "Each day supplies representative STATE; the engine decides one banner or none. "
                + "Triggers gate on player STATE, never the day alone.");

            // A fresh funnel save per day isolates each day's STATE so the walk shows the trigger
            // that day's state produces, rather than caps carried from earlier days. Cap behaviour
            // is covered by the funnel cap tests; this on-ramp demonstrates the day-to-state map.
            for (int day = 1; day <= 31; day++)
            {
                FunnelInputs inputs = InputsForDay(day, new FunnelState());
                FunnelDecision decision = engine.Evaluate(inputs);
                Debug.Log(DescribeDay(day, decision));
            }

            Debug.Log(
                "[Funnel] D1 is pure play (no trigger). After D30 a non-converter sees no new "
                + "triggers. See Keepfall ▸ Funnel ▸ Show Post-D30 Hard Branch.");
        }

        [MenuItem("Keepfall/Funnel/Show Post-D30 Hard Branch")]
        public static void ShowPostD30HardBranch()
        {
            var config = new RemoteConfig();
            var analytics = new DebugAnalytics();
            var engine = new FunnelEngine(config, analytics);
            var funnel = new FunnelState();

            // A D31 NON-converter whose STATE would otherwise satisfy several triggers. The hard
            // branch (§8.2) short-circuits the whole registry: None + funnel_postd30_suppressed.
            FunnelInputs inputs = new FunnelInputs(
                dayIndex: 31,
                nowUtc: Anchor.AddDays(31),
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
                isPlusActive: false,
                isConverter: false,
                funnel: funnel);

            FunnelDecision decision = engine.Evaluate(inputs);

            Debug.Log(
                "[Funnel] Post-D30 hard branch (§8.2). A D31 non-converter with otherwise-eligible "
                + "state asks the engine for a banner.");
            Debug.Log(
                $"[Funnel]   decision: IsNone={decision.IsNone}, Fired={decision.Fired}. "
                + "The engine emitted funnel_postd30_suppressed above and returns no new trigger.");

            // Re-evaluating does not re-emit the boundary event (once per player, persisted).
            engine.Evaluate(inputs);
            Debug.Log(
                "[Funnel]   A second pass returns None again and does not re-emit the boundary "
                + "event. Focus after D30 shifts to retention, not selling.");
        }

        // Representative STATE per the §8 day table. Day windows match the engine's eligibility
        // gates; the STATE legs are set so the day's intended trigger can fire. Days with no §8
        // trigger (D1, and the gaps between windows) carry bare state and produce None.
        private static FunnelInputs InputsForDay(int day, FunnelState funnel)
        {
            DateTimeOffset now = Anchor.AddDays(day);

            // Defaults: a quiet, progressing F2P non-converter with nothing surfaced.
            int tiles = 3;
            int roster = 6;
            long stone = 0;
            bool stoneWall = false;
            bool ownsT3 = false;
            bool specialistWall = false;
            string candidateTile = null;
            bool waitedYield = false;
            bool lostAdept = false;
            int retryTokens = 1;
            string lossSeed = null;
            int lossStreak = 0;
            bool exploring = false;
            bool pacingSlowed = false;

            // D2: waited on tile yield at least once → accelerator becomes discoverable.
            if (day == 2)
            {
                waitedYield = true;
            }

            // D3: first unlock outside starters AND first Stone wall → starter-pack banner.
            if (day == 3)
            {
                roster = 7;
                stone = 40;
                stoneWall = true;
                waitedYield = true;
            }

            // D4–D6: lost an Adept match AND has 0 retry tokens → retry-drip on the loss screen.
            if (day >= 4 && day <= 6)
            {
                roster = 8;
                lostAdept = true;
                retryTokens = 0;
            }

            // D7: week-1 checkpoint AND unlock pacing slowed → Plus reveal #1. (Engine window is
            // D7–D13; we surface reveal #1 on its first eligible day so the walk shows it once.)
            if (day == 7)
            {
                roster = 9;
                pacingSlowed = true;
                waitedYield = true;
            }

            // D8–D10: engaged & exploring synergies → Battle Pass first cycle.
            if (day >= 8 && day <= 10)
            {
                roster = 11;
                exploring = true;
            }

            // D11–D13: owns a T3 tile AND faces a specialist wall → accelerator hint near T3.
            // (Engine window is D11–D14; D14 is handed to Plus reveal #2 below.)
            if (day >= 11 && day <= 13)
            {
                roster = 13;
                ownsT3 = true;
                specialistWall = true;
                candidateTile = "T3-A";
                stoneWall = true;
            }

            // D14: two-week checkpoint, reveal #1 already shown/non-converted → Plus reveal #2.
            if (day == 14)
            {
                roster = 14;
                pacingSlowed = true;
                funnel.PlusRevealCount = 1; // reveal #1 happened earlier and did not convert
            }

            // D15–D21: 3 same-match losses → retry nudge (loss screen). We do NOT also satisfy the
            // Plus-reveal-#2 legs here, so the walk surfaces the retry nudge that owns this window.
            if (day >= 15 && day <= 21)
            {
                roster = 14;
                lossSeed = "seed-mid";
                lossStreak = 3;
                lostAdept = true;
                retryTokens = 0;
            }

            // D22–D28: roster ~18–22 → Battle Pass second cycle (and Plus reveal #3 if #1+#2 shown).
            if (day >= 22 && day <= 28)
            {
                roster = 20;
                ownsT3 = true;
            }

            // D29–D30: month-end checkpoint → thanks (no sell).
            if (day >= 29 && day <= 30)
            {
                roster = 21;
            }

            // D31: past the cliff. Non-converter → hard branch (demonstrated separately too).
            if (day >= 31)
            {
                roster = 21;
            }

            return new FunnelInputs(
                dayIndex: day,
                nowUtc: now,
                tilesOwned: tiles,
                rosterSize: roster,
                starterRosterSize: 6,
                stoneBalance: stone,
                hasHitStoneWall: stoneWall,
                ownsT3Tile: ownsT3,
                facesSpecialistWall: specialistWall,
                candidateAcceleratorTileId: candidateTile,
                hasWaitedOnTileYield: waitedYield,
                hasLostAdeptMatch: lostAdept,
                retryTokenCount: retryTokens,
                currentLossMatchSeed: lossSeed,
                currentMatchLossStreak: lossStreak,
                isExploringSynergies: exploring,
                unlockPacingSlowed: pacingSlowed,
                isPlusActive: false,
                isConverter: false,
                funnel: funnel);
        }

        private static string DescribeDay(int day, FunnelDecision decision)
        {
            if (decision.Fired)
            {
                return $"[Funnel] D{day,2}: fired {decision.Presentation.TriggerId} "
                    + $"at {Placement(decision.Presentation.Placement)} "
                    + $"(banner, dismissible={decision.Presentation.IsDismissible}, "
                    + $"modal={decision.Presentation.IsModal}).";
            }

            if (decision.Reason.HasValue)
            {
                return $"[Funnel] D{day,2}: none fired. Last suppression: "
                    + $"{decision.TriggerId} ({decision.Reason.Value.ToWireValue()}).";
            }

            return $"[Funnel] D{day,2}: none fired. No trigger was eligible for this day's state "
                + "(pure play).";
        }

        private static string Placement(FunnelPlacement placement)
        {
            switch (placement)
            {
                case FunnelPlacement.TileScreen: return "tile screen";
                case FunnelPlacement.ShopTab: return "Shop tab";
                case FunnelPlacement.LossScreen: return "loss screen";
                case FunnelPlacement.PassTab: return "Pass tab";
                case FunnelPlacement.Profile: return "Profile";
                default: return "tile screen";
            }
        }
    }
}
