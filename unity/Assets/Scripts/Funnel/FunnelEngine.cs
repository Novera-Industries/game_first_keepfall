using System;
using System.Collections.Generic;
using System.Globalization;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;

namespace Keepfall.Funnel
{
    /// <summary>
    /// The 30-day conversion-funnel trigger engine (source-of-truth §8, taxonomy §5–§6).
    ///
    /// <para>
    /// It reads player STATE (<see cref="FunnelInputs"/>, built from <see cref="PlayerState"/>)
    /// and decides which single trigger, if any, to surface. Every rule traces to the source of
    /// truth:
    /// <list type="bullet">
    ///   <item>PRECONDITION is player STATE, never wall-clock alone. <c>DayIndex</c> gates
    ///   <i>eligibility</i> but a state field must also hold (e.g. <c>d3_starter_pack</c> needs
    ///   the first unlock outside starters AND a Stone wall, SoT §8).</item>
    ///   <item>Frequency caps from RemoteConfig: Plus max 3 reveals / 30 days then 1 / month
    ///   after D30; accelerator hint max 1 / tile / week and never if an accelerator was used in
    ///   the last 7 days; retry offer only after 3 consecutive losses on the SAME match, never
    ///   on the first loss; Shard packs never auto-presented after D3 (SoT §8.2).</item>
    ///   <item>HARD BRANCH: after D30 a non-converter sees NO new triggers — the engine returns
    ///   <see cref="FunnelDecision.None"/> and emits
    ///   <see cref="Events.FunnelPostD30Suppressed"/> once at the boundary (SoT §8.2). This is an
    ///   explicit branch (<see cref="EvaluatePostD30Branch"/>), not an emergent side effect.</item>
    ///   <item>Every decision emits <see cref="Events.FunnelTriggerFired"/> or
    ///   <see cref="Events.FunnelTriggerSuppressed"/> with a reason.</item>
    ///   <item>Triggers are single dismissible <see cref="FunnelPresentation"/> banners in their
    ///   placement — never modals on app open, never countdown pressure. That is the only
    ///   presentation contract emitted.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Pure C# over Core types — no UnityEngine dependency — so the whole funnel is
    /// EditMode-testable with a <see cref="Keepfall.Core.Time.FakeTimeProvider"/> driving the
    /// clock and a recording <see cref="IAnalytics"/> capturing emissions.
    /// </para>
    /// </summary>
    public sealed class FunnelEngine
    {
        /// <summary>The D30 boundary. At or beyond this day the post-D30 branch governs.</summary>
        public const int PostD30DayIndex = 30;

        private readonly RemoteConfig _config;
        private readonly IAnalytics _analytics;

        // The evaluation order matches the SoT §8 day table: earlier-day triggers are offered
        // first when several are simultaneously eligible, so the funnel never skips a stage.
        private static readonly string[] EvaluationOrder =
        {
            TriggerIds.D2AcceleratorDiscover,
            TriggerIds.D3StarterPack,
            TriggerIds.D4RetryDrip,
            TriggerIds.D7PlusReveal1,
            TriggerIds.D8Battlepass1,
            TriggerIds.D11AccelHint,
            TriggerIds.D14PlusReveal2,
            TriggerIds.D15RetryNudge,
            TriggerIds.D22Battlepass2,
            TriggerIds.D22PlusReveal3,
            TriggerIds.D29Thanks,
        };

        /// <summary>Builds the engine over the canonical remote-config and the dual-sink analytics.</summary>
        public FunnelEngine(RemoteConfig config, IAnalytics analytics)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        /// <summary>
        /// Evaluates the whole funnel for the current player state and returns the single trigger
        /// to surface (or <see cref="FunnelDecision.None"/>). At most ONE banner fires per pass.
        ///
        /// <para>The post-D30 non-converter hard branch is checked FIRST and short-circuits the
        /// entire registry (SoT §8.2).</para>
        /// </summary>
        public FunnelDecision Evaluate(FunnelInputs inputs)
        {
            // ── HARD BRANCH (explicit): post-D30 non-converter sees NO new triggers. ──
            if (TryPostD30HardBranch(inputs, out FunnelDecision postD30Decision))
            {
                return postD30Decision;
            }

            // Walk the day-ordered registry; fire the first trigger whose STATE precondition AND
            // frequency cap both pass. Triggers that are eligible-by-day but blocked emit a
            // suppression so the dashboard sees the decline; we keep walking so a later-stage
            // trigger can still fire this pass. We remember the first suppression to surface if
            // nothing fires, giving the caller a concrete reason rather than a bare None.
            FunnelDecision? firstSuppression = null;

            foreach (string triggerId in EvaluationOrder)
            {
                FunnelDecision decision = EvaluateTrigger(triggerId, inputs);

                if (decision.Fired)
                {
                    return decision;
                }

                if (decision.Reason.HasValue && firstSuppression == null)
                {
                    firstSuppression = decision;
                }
            }

            // Nothing fired. If at least one day-eligible trigger was actively suppressed, return
            // that (already emitted). Otherwise nothing was even eligible this pass — pure None.
            return firstSuppression ?? FunnelDecision.None;
        }

        /// <summary>
        /// Evaluates a single trigger by id. Public so the UI can re-check one trigger in its own
        /// surface (e.g. the loss screen asking only about the retry nudge) and so each rule is
        /// independently unit-testable. Emits the bracketing fired/suppressed event.
        /// </summary>
        public FunnelDecision EvaluateTrigger(string triggerId, FunnelInputs inputs)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                return FunnelDecision.None;
            }

            // The post-D30 hard branch also gates every individual trigger (SoT §8.2/§6 "All §6
            // triggers above are gated behind this branch").
            if (IsPostD30NonConverter(inputs))
            {
                return Suppress(triggerId, inputs, SuppressionReason.PostD30);
            }

            switch (triggerId)
            {
                case TriggerIds.D2AcceleratorDiscover: return EvalD2AcceleratorDiscover(inputs);
                case TriggerIds.D3StarterPack: return EvalD3StarterPack(inputs);
                case TriggerIds.D4RetryDrip: return EvalD4RetryDrip(inputs);
                case TriggerIds.D7PlusReveal1: return EvalD7PlusReveal1(inputs);
                case TriggerIds.D8Battlepass1: return EvalD8Battlepass1(inputs);
                case TriggerIds.D11AccelHint: return EvalD11AccelHint(inputs);
                case TriggerIds.D14PlusReveal2: return EvalD14PlusReveal2(inputs);
                case TriggerIds.D15RetryNudge: return EvalD15RetryNudge(inputs);
                case TriggerIds.D22Battlepass2: return EvalD22Battlepass2(inputs);
                case TriggerIds.D22PlusReveal3: return EvalD22PlusReveal3(inputs);
                case TriggerIds.D29Thanks: return EvalD29Thanks(inputs);
                default: return FunnelDecision.None;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Post-D30 HARD BRANCH (explicit, SoT §8.2)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The explicit post-D30 branch, factored out so it is impossible to "fall through" into
        /// the registry. A NON-converter at D30+ gets <see cref="FunnelDecision.None"/> and a
        /// one-time <see cref="Events.FunnelPostD30Suppressed"/> at the boundary. A converter at
        /// D30+ does NOT short-circuit here — their path continues into the registry (where Plus
        /// is already handled as "already converted"), so the converter path differs by design.
        /// </summary>
        public bool TryPostD30HardBranch(FunnelInputs inputs, out FunnelDecision decision)
        {
            decision = FunnelDecision.None;

            if (!IsPostD30NonConverter(inputs))
            {
                // Converter at D30+, or anyone pre-D30: not this branch.
                return false;
            }

            EmitPostD30SuppressedOnce(inputs);
            decision = FunnelDecision.None;
            return true;
        }

        // True only for a NON-converter at or past D30 when the suppress flag is on (default true,
        // RemoteConfig funnel.postD30.suppressNewTriggers). A converter is never in this branch.
        private bool IsPostD30NonConverter(FunnelInputs inputs)
        {
            if (inputs.DayIndex < PostD30DayIndex)
            {
                return false;
            }

            if (inputs.IsConverter)
            {
                return false;
            }

            return _config.GetFunnelPostD30SuppressNewTriggers();
        }

        // Emits funnel_postd30_suppressed exactly once at the boundary (taxonomy §5: "Emitted
        // once at the boundary"). We use a sentinel key in the funnel save's per-trigger ledger so
        // the once-only guarantee survives app restarts.
        private void EmitPostD30SuppressedOnce(FunnelInputs inputs)
        {
            const string sentinel = "__postd30_suppressed_emitted";
            FunnelState funnel = inputs.Funnel;
            if (funnel.PerTriggerLastShownUtc != null
                && funnel.PerTriggerLastShownUtc.ContainsKey(sentinel))
            {
                return; // already emitted at the boundary for this player
            }

            // How many registry triggers are now switched off for this player.
            int suppressedCount = EvaluationOrder.Length;

            _analytics.Track(Events.FunnelPostD30Suppressed, new Dictionary<string, object>
            {
                ["day_index"] = inputs.DayIndex,
                ["is_converter"] = false,
                ["triggers_now_suppressed"] = suppressedCount,
            });

            if (funnel.PerTriggerLastShownUtc == null)
            {
                funnel.PerTriggerLastShownUtc = new Dictionary<string, DateTimeOffset>();
            }

            funnel.PerTriggerLastShownUtc[sentinel] = inputs.NowUtc;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Per-trigger rules (each: day-gate eligibility + STATE precondition + freq cap)
        // ─────────────────────────────────────────────────────────────────

        // D2 — accelerator discoverable (icon only). STATE: waited on tile yield ≥ once. Never
        // during the D1 first-15-min lock; on the tile screen.
        private FunnelDecision EvalD2AcceleratorDiscover(FunnelInputs inputs)
        {
            const string id = TriggerIds.D2AcceleratorDiscover;
            // Early-phase discoverability: eligible from D2 up to (but not into) the D7 Plus
            // window. A player who only reaches this state much later is past the discovery beat;
            // the accelerator is anyway always reachable from the tile UI by then.
            if (inputs.DayIndex < 1 || inputs.DayIndex > 6)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (!inputs.HasWaitedOnTileYield)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            // Show once (discoverability icon); thereafter the accelerator hint cap governs.
            if (HasShown(inputs.Funnel, id))
            {
                return Suppress(id, inputs, SuppressionReason.FreqCapHit);
            }

            return Fire(id, FunnelPlacement.TileScreen, inputs);
        }

        // D3 — $0.99 Shard starter pack (single Shop banner). STATE: first unlock outside the 6
        // starters AND first Stone wall hit. Shown once; Shard packs otherwise live only in the
        // Shop, never auto-presented after D3 (SoT §7, §8.2).
        private FunnelDecision EvalD3StarterPack(FunnelInputs inputs)
        {
            const string id = TriggerIds.D3StarterPack;
            // SoT §8.2 / §7: the starter-pack banner is a D3 beat ONLY — Shard packs are never
            // auto-presented after D3 (they remain always-visible in the Shop tab, but that is
            // the shop's surface, not a funnel trigger). So the upper bound is D3.
            if (inputs.DayIndex != 3)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            // The canonical STATE gate (the gating test asserts D3 alone is not enough).
            if (!inputs.HasUnlockedOutsideStarters || !inputs.HasHitStoneWall)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (HasShown(inputs.Funnel, id))
            {
                return Suppress(id, inputs, SuppressionReason.FreqCapHit);
            }

            return Fire(id, FunnelPlacement.ShopTab, inputs);
        }

        // D4–D6 — retry-token drip on the loss screen. STATE: lost ≥ 1 Adept (tier 2) match AND
        // has 0 retry tokens. Cosmetic, login-granted drip — never on a first-ever loss with
        // tokens in hand.
        private FunnelDecision EvalD4RetryDrip(FunnelInputs inputs)
        {
            const string id = TriggerIds.D4RetryDrip;
            if (inputs.DayIndex < 4 || inputs.DayIndex > 6)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (!inputs.HasLostAdeptMatch || inputs.RetryTokenCount > 0)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (ShownWithinDays(inputs.Funnel, id, inputs.NowUtc, 1))
            {
                return Suppress(id, inputs, SuppressionReason.FreqCapHit);
            }

            return Fire(id, FunnelPlacement.LossScreen, inputs);
        }

        // D7 — Keepfall Plus first reveal. STATE: week-1 checkpoint AND unlock pacing slowed.
        // Not if already Plus. Counts against the 3-per-30-day Plus reveal cap.
        private FunnelDecision EvalD7PlusReveal1(FunnelInputs inputs)
        {
            const string id = TriggerIds.D7PlusReveal1;
            // Week-1 checkpoint beat: D7 up to (but not into) the D14 reveal-#2 window.
            if (inputs.DayIndex < 7 || inputs.DayIndex > 13)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (!inputs.UnlockPacingSlowed)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            return EvaluatePlusReveal(id, inputs);
        }

        // D8–D10 — Battle Pass first cycle. STATE: engaged & exploring synergies. Once per cycle.
        private FunnelDecision EvalD8Battlepass1(FunnelInputs inputs)
        {
            const string id = TriggerIds.D8Battlepass1;
            if (inputs.DayIndex < 8 || inputs.DayIndex > 10)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (!inputs.IsExploringSynergies)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (HasShown(inputs.Funnel, id))
            {
                return Suppress(id, inputs, SuppressionReason.FreqCapHit);
            }

            return Fire(id, FunnelPlacement.PassTab, inputs);
        }

        // D11–D14 — yield accelerator hint near a T3 tile. STATE: owns a T3 tile AND faces a
        // specialist Stone wall. Cap: max 1 per tile per week; NEVER to a player who used an
        // accelerator in the past 7 days (SoT §8.2).
        private FunnelDecision EvalD11AccelHint(FunnelInputs inputs)
        {
            const string id = TriggerIds.D11AccelHint;
            if (inputs.DayIndex < 11 || inputs.DayIndex > 14)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (!inputs.OwnsT3Tile || !inputs.FacesSpecialistWall
                || string.IsNullOrEmpty(inputs.CandidateAcceleratorTileId))
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            return EvaluateAcceleratorHint(id, inputs);
        }

        // D14 — Plus reveal #2 (personalized framing). STATE: two-week checkpoint AND reveal #1
        // did not convert (i.e. still not Plus and a prior reveal exists). 3-per-30 cap applies.
        private FunnelDecision EvalD14PlusReveal2(FunnelInputs inputs)
        {
            const string id = TriggerIds.D14PlusReveal2;
            // Two-week checkpoint beat: D14 up to (but not into) the D22 reveal-#3 window.
            if (inputs.DayIndex < 14 || inputs.DayIndex > 21)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            // Reveal #2 presupposes reveal #1 happened and did not convert.
            if (inputs.Funnel.PlusRevealCount < 1)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            return EvaluatePlusReveal(id, inputs);
        }

        // D15–D21 — retry nudge. STATE: 3 consecutive losses on the SAME match seed. NEVER on
        // the first loss (SoT §8.2). Loss screen.
        private FunnelDecision EvalD15RetryNudge(FunnelInputs inputs)
        {
            const string id = TriggerIds.D15RetryNudge;
            if (inputs.DayIndex < 15 || inputs.DayIndex > 21)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            return EvaluateRetryNudge(id, inputs);
        }

        // D22–D28 — Battle Pass second cycle. STATE: roster ~18–22 / 24 AND second cycle live.
        private FunnelDecision EvalD22Battlepass2(FunnelInputs inputs)
        {
            const string id = TriggerIds.D22Battlepass2;
            if (inputs.DayIndex < 22 || inputs.DayIndex > 28)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (inputs.RosterSize < 18 || inputs.RosterSize > 22)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (HasShown(inputs.Funnel, id))
            {
                return Suppress(id, inputs, SuppressionReason.FreqCapHit);
            }

            return Fire(id, FunnelPlacement.PassTab, inputs);
        }

        // D22–D28 — Plus reveal #3 (final). STATE: reveals #1 AND #2 both dismissed/non-converted
        // AND still not Plus. 3-per-30 cap applies (this is the 3rd).
        private FunnelDecision EvalD22PlusReveal3(FunnelInputs inputs)
        {
            const string id = TriggerIds.D22PlusReveal3;
            if (inputs.DayIndex < 22 || inputs.DayIndex > 28)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            // Only after #1 and #2 were both shown and neither converted.
            if (inputs.Funnel.PlusRevealCount < 2)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            return EvaluatePlusReveal(id, inputs);
        }

        // D29–D30 — month-end thanks. STATE: month-end checkpoint. NO sell — carries no purchase
        // surface; the banner copy thanks the player (free Shard drop + cosmetic handled by the
        // economy layer on fire). Once.
        private FunnelDecision EvalD29Thanks(FunnelInputs inputs)
        {
            const string id = TriggerIds.D29Thanks;
            if (inputs.DayIndex < 29 || inputs.DayIndex >= PostD30DayIndex + 1)
            {
                return Suppress(id, inputs, SuppressionReason.PreconditionUnmet);
            }

            if (HasShown(inputs.Funnel, id))
            {
                return Suppress(id, inputs, SuppressionReason.FreqCapHit);
            }

            return Fire(id, FunnelPlacement.Profile, inputs);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Shared cap evaluators (Plus reveal · accelerator hint · retry nudge)
        // ─────────────────────────────────────────────────────────────────

        // Plus reveals: not if already Plus; max N (default 3) per cooldown window (default 30
        // days); placement is Shop for #1, Profile for #2/#3 (SoT §8 table). On fire, the reveal
        // counter is bumped so the next reveal trigger sees the right precondition.
        private FunnelDecision EvaluatePlusReveal(string triggerId, FunnelInputs inputs)
        {
            if (inputs.IsPlusActive || inputs.IsConverter)
            {
                return Suppress(triggerId, inputs, SuppressionReason.AlreadyConverted);
            }

            int maxReveals = _config.GetFunnelPlusMaxReveals();
            int cooldownDays = _config.GetFunnelPlusCooldownDays();
            int revealsInWindow = CountPlusRevealsInWindow(inputs, cooldownDays);
            if (revealsInWindow >= maxReveals)
            {
                return Suppress(triggerId, inputs, SuppressionReason.FreqCapHit);
            }

            // A given reveal trigger fires once (it has its own day window); re-asking the same
            // reveal id is capped here too.
            if (HasShown(inputs.Funnel, triggerId))
            {
                return Suppress(triggerId, inputs, SuppressionReason.FreqCapHit);
            }

            FunnelPlacement placement = triggerId == TriggerIds.D7PlusReveal1
                ? FunnelPlacement.ShopTab
                : FunnelPlacement.Profile;

            FunnelDecision decision = Fire(triggerId, placement, inputs);
            inputs.Funnel.PlusRevealCount += 1;
            return decision;
        }

        // Counts Plus reveals shown within the trailing cooldown window. After D30 this naturally
        // collapses to "once per month" because only the long window is consulted (SoT §8.2).
        private int CountPlusRevealsInWindow(FunnelInputs inputs, int cooldownDays)
        {
            DateTimeOffset cutoff = inputs.NowUtc - TimeSpan.FromDays(cooldownDays);
            int count = 0;
            Dictionary<string, DateTimeOffset> ledger = inputs.Funnel.PerTriggerLastShownUtc;
            if (ledger == null)
            {
                return 0;
            }

            foreach (string revealId in PlusRevealTriggerIds)
            {
                if (ledger.TryGetValue(revealId, out DateTimeOffset shownUtc) && shownUtc >= cutoff)
                {
                    count++;
                }
            }

            return count;
        }

        private static readonly string[] PlusRevealTriggerIds =
        {
            TriggerIds.D7PlusReveal1,
            TriggerIds.D14PlusReveal2,
            TriggerIds.D22PlusReveal3,
        };

        // Accelerator hint: max N per tile per week (funnel.accelHint.maxPerTilePerWeek, default 1)
        // AND never if an accelerator was used in the post-use suppression window
        // (funnel.accelHint.cooldownDays, default 7) (SoT §8.2). The per-tile weekly cap reads the
        // FunnelState per-tile ledger; the recent-use check reads the same ledger sentinel the
        // accelerator feature writes on use (see NoteAcceleratorUsed).
        private const int AccelHintWeekDays = 7;

        private FunnelDecision EvaluateAcceleratorHint(string triggerId, FunnelInputs inputs)
        {
            int cooldownDays = _config.GetFunnelAcceleratorHintCooldownDays();
            int maxPerTilePerWeek = _config.GetFunnelAccelHintMaxPerTilePerWeek();
            string tileId = inputs.CandidateAcceleratorTileId;

            // Never to a player who used an accelerator within the post-use suppression window.
            if (UsedAcceleratorWithinDays(inputs.Funnel, inputs.NowUtc, cooldownDays))
            {
                return Suppress(triggerId, inputs, SuppressionReason.RecentlyUsedAccelerator);
            }

            // Max N hints per THIS tile per (7-day) week. The per-tile ledger holds the last hint
            // time, so a hint inside the trailing week counts as one against the weekly cap.
            if (inputs.Funnel.PerTileAcceleratorHintUtc != null
                && inputs.Funnel.PerTileAcceleratorHintUtc.TryGetValue(tileId, out DateTimeOffset last)
                && inputs.NowUtc - last < TimeSpan.FromDays(AccelHintWeekDays)
                && maxPerTilePerWeek <= 1)
            {
                return Suppress(triggerId, inputs, SuppressionReason.FreqCapHit);
            }

            FunnelDecision decision = Fire(triggerId, FunnelPlacement.TileScreen, inputs);

            // Record the per-tile hint timestamp for the weekly cap.
            if (inputs.Funnel.PerTileAcceleratorHintUtc == null)
            {
                inputs.Funnel.PerTileAcceleratorHintUtc = new Dictionary<string, DateTimeOffset>();
            }

            inputs.Funnel.PerTileAcceleratorHintUtc[tileId] = inputs.NowUtc;
            return decision;
        }

        // Retry nudge: only after `required` (default 3) consecutive losses on the SAME match
        // seed; NEVER on the first loss (SoT §8.2). The streak count comes from RetryState via
        // FunnelInputs, so the rule reads real per-match state, not a day.
        private FunnelDecision EvaluateRetryNudge(string triggerId, FunnelInputs inputs)
        {
            if (string.IsNullOrEmpty(inputs.CurrentLossMatchSeed))
            {
                return Suppress(triggerId, inputs, SuppressionReason.PreconditionUnmet);
            }

            int required = _config.GetFunnelRetryLossStreakRequired();
            if (inputs.CurrentMatchLossStreak < required)
            {
                // Explicitly the "never on first loss / not enough same-match losses" rule.
                return Suppress(triggerId, inputs, SuppressionReason.NotFirstLossRule);
            }

            return Fire(triggerId, FunnelPlacement.LossScreen, inputs);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Bookkeeping hooks the feature layer calls (keep caps honest across restarts)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Records that the player USED an accelerator at <paramref name="whenUtc"/>. The
        /// accelerator feature MUST call this so the "never hint within 7 days of a use" rule
        /// (SoT §8.2) survives app restarts. Stored as a sentinel in the funnel per-trigger
        /// ledger.
        /// </summary>
        public static void NoteAcceleratorUsed(FunnelState funnel, DateTimeOffset whenUtc)
        {
            if (funnel == null)
            {
                return;
            }

            if (funnel.PerTriggerLastShownUtc == null)
            {
                funnel.PerTriggerLastShownUtc = new Dictionary<string, DateTimeOffset>();
            }

            funnel.PerTriggerLastShownUtc[AcceleratorUsedSentinel] = whenUtc;
        }

        private const string AcceleratorUsedSentinel = "__accelerator_last_used";

        private static bool UsedAcceleratorWithinDays(
            FunnelState funnel, DateTimeOffset nowUtc, int days)
        {
            if (funnel.PerTriggerLastShownUtc != null
                && funnel.PerTriggerLastShownUtc.TryGetValue(
                    AcceleratorUsedSentinel, out DateTimeOffset usedUtc))
            {
                return nowUtc - usedUtc < TimeSpan.FromDays(days);
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Fire / Suppress (the only places events + state writes happen)
        // ─────────────────────────────────────────────────────────────────

        private FunnelDecision Fire(string triggerId, FunnelPlacement placement, FunnelInputs inputs)
        {
            // Record last-shown for per-trigger caps and emit the fired event.
            if (inputs.Funnel.PerTriggerLastShownUtc == null)
            {
                inputs.Funnel.PerTriggerLastShownUtc = new Dictionary<string, DateTimeOffset>();
            }

            inputs.Funnel.PerTriggerLastShownUtc[triggerId] = inputs.NowUtc;

            _analytics.Track(Events.FunnelTriggerFired, new Dictionary<string, object>
            {
                ["trigger_id"] = triggerId,
                ["day_index"] = inputs.DayIndex,
                ["placement"] = PlacementWire(placement),
                ["state_snapshot"] = BuildStateSnapshot(inputs),
                ["fire_count_30d"] = CountFiresInWindow(inputs, triggerId, 30),
            });

            var presentation = new FunnelPresentation(
                triggerId, placement, FunnelCopy.ForTrigger(triggerId));
            return FunnelDecision.FromFired(presentation);
        }

        private FunnelDecision Suppress(
            string triggerId, FunnelInputs inputs, SuppressionReason reason)
        {
            _analytics.Track(Events.FunnelTriggerSuppressed, new Dictionary<string, object>
            {
                ["trigger_id"] = triggerId,
                ["day_index"] = inputs.DayIndex,
                ["reason"] = reason.ToWireValue(),
            });

            return FunnelDecision.FromSuppressed(triggerId, reason);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Small helpers
        // ─────────────────────────────────────────────────────────────────

        private static bool HasShown(FunnelState funnel, string triggerId)
        {
            return funnel.PerTriggerLastShownUtc != null
                && funnel.PerTriggerLastShownUtc.ContainsKey(triggerId);
        }

        private static bool ShownWithinDays(
            FunnelState funnel, string triggerId, DateTimeOffset nowUtc, int days)
        {
            if (funnel.PerTriggerLastShownUtc != null
                && funnel.PerTriggerLastShownUtc.TryGetValue(triggerId, out DateTimeOffset last))
            {
                return nowUtc - last < TimeSpan.FromDays(days);
            }

            return false;
        }

        // A single trigger fires at most once per its day window in Phase 1, but the dashboard
        // still wants a 30-day fire count for the funnel-health overlay (taxonomy §7). We count
        // the last-shown timestamp if it lies in the window (so this is 0 or 1 per trigger).
        private static int CountFiresInWindow(FunnelInputs inputs, string triggerId, int days)
        {
            return ShownWithinDays(inputs.Funnel, triggerId, inputs.NowUtc, days) ? 1 : 0;
        }

        private static string BuildStateSnapshot(FunnelInputs inputs)
        {
            // Compact JSON matching the taxonomy §5 example shape: {"tiles":4,"roster":9,...}.
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tiles\":{0},\"roster\":{1},\"loss_streak\":{2},\"stone\":{3},\"plus\":{4}}}",
                inputs.TilesOwned,
                inputs.RosterSize,
                inputs.CurrentMatchLossStreak,
                inputs.StoneBalance,
                inputs.IsPlusActive ? "true" : "false");
        }

        private static string PlacementWire(FunnelPlacement placement)
        {
            switch (placement)
            {
                case FunnelPlacement.TileScreen: return "tile_screen";
                case FunnelPlacement.ShopTab: return "shop_tab";
                case FunnelPlacement.LossScreen: return "loss_screen";
                case FunnelPlacement.PassTab: return "pass_tab";
                case FunnelPlacement.Profile: return "profile";
                default: return "tile_screen";
            }
        }
    }
}
