namespace Keepfall.Funnel
{
    /// <summary>
    /// The ONLY presentation contract the funnel engine emits. Every surfaced trigger is a
    /// <b>single, dismissible banner</b> in a named in-context <see cref="FunnelPlacement"/> —
    /// never a modal, never a countdown, never an app-open interstitial (source-of-truth §8,
    /// §10.5–§10.7; taxonomy §6 never-fire guardrails).
    ///
    /// <para>
    /// The shape itself encodes the guardrails so a UI layer physically cannot render the
    /// banned forms from an engine decision:
    /// <list type="bullet">
    ///   <item><see cref="IsModal"/> is hard-wired <c>false</c> — there is no way to ask the
    ///   engine for a modal.</item>
    ///   <item><see cref="IsDismissible"/> is hard-wired <c>true</c> — every banner can be
    ///   dismissed.</item>
    ///   <item>There is <b>no</b> countdown / expiry / "limited time" field — the contract
    ///   carries no pressure timer (SoT §10.7).</item>
    /// </list>
    /// </para>
    /// </summary>
    public readonly struct FunnelPresentation
    {
        /// <summary>The trigger id (one of <see cref="Keepfall.Analytics.TriggerIds"/>).</summary>
        public readonly string TriggerId;

        /// <summary>Where the banner appears. A closed set with no app-open value.</summary>
        public readonly FunnelPlacement Placement;

        /// <summary>Calm, second-person banner copy (SoT §12 — no exclamation points, no shouting).</summary>
        public readonly string BodyCopy;

        /// <summary>Always a banner, never a modal (SoT §10.5/§10.6). Hard-wired.</summary>
        public bool IsModal => false;

        /// <summary>Always dismissible (SoT §8 "single, dismissible banner"). Hard-wired.</summary>
        public bool IsDismissible => true;

        /// <summary>Builds the single-banner presentation for a fired trigger.</summary>
        public FunnelPresentation(string triggerId, FunnelPlacement placement, string bodyCopy)
        {
            TriggerId = triggerId;
            Placement = placement;
            BodyCopy = bodyCopy;
        }
    }
}
