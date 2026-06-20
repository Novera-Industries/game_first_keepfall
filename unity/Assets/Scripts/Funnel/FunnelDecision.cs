namespace Keepfall.Funnel
{
    /// <summary>
    /// Outcome of evaluating a single trigger (or an evaluation pass). Exactly one of three
    /// shapes:
    /// <list type="bullet">
    ///   <item><see cref="Fired"/> — the precondition + frequency cap passed; a single
    ///   dismissible <see cref="Presentation"/> is returned and
    ///   <see cref="Keepfall.Analytics.Events.FunnelTriggerFired"/> was emitted.</item>
    ///   <item>Suppressed — the engine declined; <see cref="Reason"/> is set and
    ///   <see cref="Keepfall.Analytics.Events.FunnelTriggerSuppressed"/> was emitted.</item>
    ///   <item><see cref="None"/> — nothing was eligible to evaluate (no trigger matched the
    ///   day/state at all). No event is emitted for a pure None pass.</item>
    /// </list>
    /// </summary>
    public readonly struct FunnelDecision
    {
        /// <summary>True when a trigger was surfaced; <see cref="Presentation"/> is valid.</summary>
        public readonly bool Fired;

        /// <summary>The single dismissible banner to show (only meaningful when <see cref="Fired"/>).</summary>
        public readonly FunnelPresentation Presentation;

        /// <summary>The trigger id this decision concerns, or null for a pure <see cref="None"/> pass.</summary>
        public readonly string TriggerId;

        /// <summary>Why it was suppressed (only meaningful when not <see cref="Fired"/> and not None).</summary>
        public readonly SuppressionReason? Reason;

        private FunnelDecision(
            bool fired, FunnelPresentation presentation, string triggerId, SuppressionReason? reason)
        {
            Fired = fired;
            Presentation = presentation;
            TriggerId = triggerId;
            Reason = reason;
        }

        /// <summary>True when the engine evaluated nothing and surfaced nothing.</summary>
        public bool IsNone => !Fired && Reason == null;

        /// <summary>Builds a fired decision carrying the banner to present.</summary>
        public static FunnelDecision FromFired(FunnelPresentation presentation) =>
            new FunnelDecision(true, presentation, presentation.TriggerId, null);

        /// <summary>Builds a suppressed decision for <paramref name="triggerId"/> with a reason.</summary>
        public static FunnelDecision FromSuppressed(string triggerId, SuppressionReason reason) =>
            new FunnelDecision(false, default, triggerId, reason);

        /// <summary>The "nothing eligible" decision.</summary>
        public static readonly FunnelDecision None = new FunnelDecision(false, default, null, null);
    }
}
