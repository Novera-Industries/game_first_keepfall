namespace Keepfall.Core.Pvp
{
    /// <summary>
    /// INERT PLACEHOLDER — DO NOT IMPLEMENT IN PHASE 1.
    ///
    /// Phase 1 is single-player PvE ONLY (source-of-truth §0). PvP arrives in Phase 2 and is
    /// greenlit only if D30 ≥ 8% AND ARPDAU ≥ $0.25 (§9). This type exists solely to mark the
    /// seam where async-PvP matchmaking would later hook in, so future work has a named anchor
    /// without scattering "TODO PvP" through the codebase. It has no behaviour, ships disabled,
    /// and must never be wired into combat, monetization, or the funnel during Phase 1.
    ///
    /// Any attempt to add real PvP logic here in Phase 1 fails the PR per the scope guardrail.
    /// </summary>
    public static class PvpPlaceholder
    {
        /// <summary>Always false in Phase 1. Guards any accidental PvP entry points.</summary>
        public const bool Enabled = false;
    }
}
