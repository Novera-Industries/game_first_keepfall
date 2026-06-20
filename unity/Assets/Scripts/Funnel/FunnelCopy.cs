namespace Keepfall.Funnel
{
    /// <summary>
    /// Canonical banner copy for each funnel trigger. Tone obeys source-of-truth §12: calm,
    /// honest, second-person, no shouting, <b>no exclamation points</b>, no confetti, no
    /// "limited time" pressure (SoT §10.7). Each line states the convenience plainly and lets
    /// the player decide — these sell TIME the player has earned, never an outcome.
    /// </summary>
    public static class FunnelCopy
    {
        /// <summary>D2 — accelerator becomes discoverable (icon only, not an offer).</summary>
        public const string D2AcceleratorDiscover =
            "Tiles fill on their own over time. You can also fill one now if you would rather not wait.";

        /// <summary>D3 — $0.99 Shard starter pack, single Shop banner.</summary>
        public const string D3StarterPack =
            "A small Shard pack is available in the Shop if you want a little extra to spend.";

        /// <summary>D4–D6 — retry tokens via daily-login drip on the loss screen.</summary>
        public const string D4RetryDrip =
            "You have a retry token from your daily login. It restores this match exactly as it was.";

        /// <summary>D7 — Keepfall Plus first reveal.</summary>
        public const string D7PlusReveal1 =
            "Keepfall Plus speeds up tile yield and adds a deck slot. It is in the Shop whenever you want it.";

        /// <summary>D8–D10 — Battle Pass first cycle reveal.</summary>
        public const string D8Battlepass1 =
            "The Battle Pass season is underway. Both tracks are cosmetic, and the free track is yours to complete.";

        /// <summary>D11–D14 — yield accelerator hint near a T3 tile.</summary>
        public const string D11AccelHint =
            "This tile is well along. You can fill it to its cap now if you are saving toward a specialist.";

        /// <summary>D14 — Plus reveal #2, personalized value framing.</summary>
        public const string D14PlusReveal2 =
            "Keepfall Plus would shorten your road to a full roster by a couple of weeks. The choice stays yours.";

        /// <summary>D15–D21 — retry nudge after 3 consecutive same-match losses.</summary>
        public const string D15RetryNudge =
            "This match has been close three times. A retry token restores the same fight if you want another go.";

        /// <summary>D22–D28 — Battle Pass second cycle reveal.</summary>
        public const string D22Battlepass2 =
            "A new Battle Pass cycle is live. The rewards are cosmetic, on both tracks.";

        /// <summary>D22–D28 — Plus reveal #3 (final).</summary>
        public const string D22PlusReveal3 =
            "If Keepfall Plus fits how you play, it is still in the Shop. This is the last time we will mention it for a while.";

        /// <summary>D29–D30 — month-end thanks. No sell.</summary>
        public const string D29Thanks =
            "Thank you for a month with Keepfall. Here is a small Shard drop and a cosmetic, with our gratitude.";

        /// <summary>Returns the canonical copy for a trigger id, or empty if unknown.</summary>
        public static string ForTrigger(string triggerId)
        {
            switch (triggerId)
            {
                case Analytics.TriggerIds.D2AcceleratorDiscover: return D2AcceleratorDiscover;
                case Analytics.TriggerIds.D3StarterPack: return D3StarterPack;
                case Analytics.TriggerIds.D4RetryDrip: return D4RetryDrip;
                case Analytics.TriggerIds.D7PlusReveal1: return D7PlusReveal1;
                case Analytics.TriggerIds.D8Battlepass1: return D8Battlepass1;
                case Analytics.TriggerIds.D11AccelHint: return D11AccelHint;
                case Analytics.TriggerIds.D14PlusReveal2: return D14PlusReveal2;
                case Analytics.TriggerIds.D15RetryNudge: return D15RetryNudge;
                case Analytics.TriggerIds.D22Battlepass2: return D22Battlepass2;
                case Analytics.TriggerIds.D22PlusReveal3: return D22PlusReveal3;
                case Analytics.TriggerIds.D29Thanks: return D29Thanks;
                default: return string.Empty;
            }
        }
    }
}
