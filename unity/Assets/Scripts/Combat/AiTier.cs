namespace Keepfall.Combat
{
    /// <summary>
    /// The five PvE AI difficulty tiers (source-of-truth §4). Difficulty advances on ROSTER
    /// EXPANSION — how many units the player has unlocked — NEVER on raw days played (§4: "the
    /// day ranges are the expected correlation, not the trigger"). The unlock-count thresholds
    /// live in RemoteConfig (keys <c>ai.threshold.*</c>) so tuning needs no rebuild (§11).
    ///
    /// TUNING INTENT — unassisted-F2P loss-rate targets (§4):
    ///   Apprentice ≤ 25% · Adept ~30% (interpolated) · Tactician ~40% · Commander ~48%
    ///   (interpolated) · Marshal ~55%. These are the design goals the AI behavior and the
    ///   <c>combat.ai.*</c> reaction/error knobs should be tuned to hit during balancing;
    ///   they are documented here so behavior changes can be checked against intent.
    /// </summary>
    public enum AiTier
    {
        /// <summary>Reactive, weak synergies. Target loss-rate ≤ 25%. Expected D1–D3.</summary>
        Apprentice = 0,

        /// <summary>Mixed offense/defense, predictable cycles. ~30%. Expected D4–D7.</summary>
        Adept = 1,

        /// <summary>Strong cycle management, baits well. Target ~40%. Expected D8–D14.</summary>
        Tactician = 2,

        /// <summary>Reads decks, deck-specific counters. ~48%. Expected D15–D25.</summary>
        Commander = 3,

        /// <summary>Punishes mistakes, near-optimal. Target ~55%. Expected D25+.</summary>
        Marshal = 4,
    }
}
