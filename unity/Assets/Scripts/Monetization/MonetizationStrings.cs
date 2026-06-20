namespace Keepfall.Monetization
{
    /// <summary>
    /// Every user-facing string the Monetization assembly emits, in one place so the tone is
    /// auditable against the source-of-truth §12 copy rules: calm, honest, second-person, no
    /// shouting, NO exclamation points, no confetti, no "buy or lose forever" pressure.
    ///
    /// These are seam strings the UI layer renders verbatim; keeping them here (rather than
    /// scattered) makes the no-exclamation-points guarantee a single grep target and lets a
    /// future localization pass key off stable identifiers. None of these strings sell an
    /// outcome — they describe time the player already earned being compressed.
    /// </summary>
    public static class MonetizationStrings
    {
        // ── Yield Accelerator (Product 1, §6) ────────────────────────────
        /// <summary>Calm confirmation after a successful accelerate. Mirrors the §2 claim tone.</summary>
        public const string AcceleratorFilled =
            "Tile filled to its current cap. Stone is ready to claim.";

        /// <summary>Shown when the tile is below the 30% reveal threshold.</summary>
        public const string AcceleratorNeedsMoreFill =
            "This tile has not accrued enough yet to accelerate.";

        /// <summary>Shown during the first-15-minutes-of-D1 lock.</summary>
        public const string AcceleratorLockedEarly =
            "Acceleration becomes available a little later in your first day.";

        /// <summary>Shown when accelerating would push queued yield past the 3-day cap.</summary>
        public const string AcceleratorWouldStackTooFar =
            "You already have plenty of Stone queued here. Claim some before accelerating again.";

        /// <summary>Shown when the wallet cannot afford the Shard price.</summary>
        public const string AcceleratorNotEnoughShards =
            "You do not have enough Shards for this yet.";

        /// <summary>Shown when the tile is already at or above its cap (nothing to fill).</summary>
        public const string AcceleratorAlreadyAtCap =
            "This tile is already full. Claim it to start a fresh yield.";

        // ── Keepfall Plus (Product 2, §6) ────────────────────────────────
        /// <summary>Honest, single-line value framing. One tier, no scaling.</summary>
        public const string PlusValue =
            "Keepfall Plus speeds up the Stone you already earn and adds loadout room. It never sells power.";

        /// <summary>Trial framing when the remote-config flag is enabled.</summary>
        public const string PlusTrialAvailable =
            "You can try Keepfall Plus free for seven days. Cancel any time and keep every cosmetic you earned.";

        /// <summary>Reassurance shown around the cancel flow (trust commitment, §6).</summary>
        public const string PlusCosmeticsKept =
            "Cosmetics you earned while subscribed stay yours after you cancel.";

        // ── Retry tokens (Product 3, §6) — calm, never coercive ──────────
        /// <summary>Neutral framing for the retry offer (only ever shown after 3 losses, §8).</summary>
        public const string RetryOffer =
            "A retry token restores this match exactly as it was. You still have to win it.";

        /// <summary>Mapped from the server's "cannot_retry_a_win" verdict.</summary>
        public const string RetryRefusedWin =
            "You cannot retry a match you already won.";

        /// <summary>Mapped from the server's "cannot_retry_a_retry" verdict.</summary>
        public const string RetryRefusedRetry =
            "You cannot retry a retry attempt.";

        /// <summary>Mapped from the server's "insufficient_tokens" verdict.</summary>
        public const string RetryRefusedNoTokens =
            "You have no retry tokens. A daily login grants one.";

        /// <summary>Generic, calm fallback for any other server refusal.</summary>
        public const string RetryRefusedGeneric =
            "This retry is not available right now.";
    }
}
