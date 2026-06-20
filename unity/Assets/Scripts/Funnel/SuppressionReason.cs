namespace Keepfall.Funnel
{
    /// <summary>
    /// Why the engine declined to show a trigger. These map 1:1 to the
    /// <c>funnel_trigger_suppressed.reason</c> enum domain in the taxonomy (§5) and the
    /// post-D30 hard branch (§5/§8.2). <see cref="ToWireValue"/> yields the exact wire string
    /// the dashboard joins on — do not change those strings once shipped.
    /// </summary>
    public enum SuppressionReason
    {
        /// <summary>Player STATE precondition not satisfied (e.g. no first-unlock-outside-starters).</summary>
        PreconditionUnmet = 0,

        /// <summary>A frequency cap blocked it (e.g. Plus already shown 3× in 30 days).</summary>
        FreqCapHit = 1,

        /// <summary>Accelerator hint suppressed: player used an accelerator in the past 7 days.</summary>
        RecentlyUsedAccelerator = 2,

        /// <summary>Player has already converted (Plus or meaningful spend) — no need to nudge.</summary>
        AlreadyConverted = 3,

        /// <summary>Retry offer would violate "never on first loss / not 3 same-match losses yet".</summary>
        NotFirstLossRule = 4,

        /// <summary>The post-D30 non-converter hard branch is active (SoT §8.2).</summary>
        PostD30 = 5,
    }

    /// <summary>Wire-string mapping for <see cref="SuppressionReason"/> (taxonomy §5 enum domain).</summary>
    public static class SuppressionReasonExtensions
    {
        /// <summary>The exact <c>reason</c> string emitted on <c>funnel_trigger_suppressed</c>.</summary>
        public static string ToWireValue(this SuppressionReason reason)
        {
            switch (reason)
            {
                case SuppressionReason.PreconditionUnmet: return "precondition_unmet";
                case SuppressionReason.FreqCapHit: return "freq_cap_hit";
                case SuppressionReason.RecentlyUsedAccelerator: return "recently_used_accelerator";
                case SuppressionReason.AlreadyConverted: return "already_converted";
                case SuppressionReason.NotFirstLossRule: return "not_first_loss_rule";
                case SuppressionReason.PostD30: return "post_d30";
                default: return "precondition_unmet";
            }
        }
    }
}
