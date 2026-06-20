namespace Keepfall.Combat
{
    /// <summary>
    /// The silent post-match stat summary (source-of-truth §4 "post-match: silent stat summary"
    /// and §12 tone). This is DATA ONLY — it carries the numbers the result screen shows. Every
    /// string here is calm, honest, second-person, and contains NO exclamation points and NO
    /// confetti language (§12). There is no "You crushed them" copy; we report, we do not shout.
    ///
    /// Reward gating note (§6 Product 3): whether THIS match grants first-attempt rewards is a
    /// SERVER decision (the Cloudflare Worker is the authority — it enforces "cannot retry a win,
    /// cannot retry a retry, rewards capped at first attempt"). This summary only reflects what
    /// the server already decided via <see cref="RewardsGranted"/>; it never grants anything.
    /// </summary>
    public sealed class PostMatchSummary
    {
        /// <summary>Decided outcome (§4).</summary>
        public MatchOutcome Outcome { get; }

        /// <summary>AI tier this match was played against (§4).</summary>
        public AiTier OpponentTier { get; }

        /// <summary>Enemy towers the player destroyed (0..3).</summary>
        public int EnemyTowersDestroyed { get; }

        /// <summary>Player towers the AI destroyed (0..3).</summary>
        public int PlayerTowersLost { get; }

        /// <summary>Total tower damage the player dealt.</summary>
        public int DamageDealt { get; }

        /// <summary>Total tower damage the player took.</summary>
        public int DamageTaken { get; }

        /// <summary>Match length in whole seconds (≤ 180).</summary>
        public int DurationSeconds { get; }

        /// <summary>True if this was a server-validated retry attempt (rewards do not re-grant).</summary>
        public bool WasRetry { get; }

        /// <summary>Whether the server granted rewards for this attempt (first-attempt only, §6).</summary>
        public bool RewardsGranted { get; }

        /// <summary>Builds a summary from a resolved match.</summary>
        public PostMatchSummary(
            MatchState state,
            MatchOutcome outcome,
            AiTier opponentTier,
            bool wasRetry,
            bool rewardsGranted)
        {
            Outcome = outcome;
            OpponentTier = opponentTier;
            EnemyTowersDestroyed = state.DestroyedEnemyTowers;
            PlayerTowersLost = state.DestroyedPlayerTowers;
            DamageDealt = state.DamageDealtToEnemy;
            DamageTaken = state.DamageDealtToPlayer;
            DurationSeconds = (int)state.ElapsedSeconds;
            WasRetry = wasRetry;
            RewardsGranted = rewardsGranted;
        }

        /// <summary>
        /// A calm, second-person headline for the result screen (§12 — no exclamation points,
        /// no confetti). Reports the outcome plainly; the tile-claim line is appended by the
        /// economy layer when a win earns a tile (e.g. "You claimed Tile 07. Stone yield begins
        /// now.") — that text is not duplicated here.
        /// </summary>
        public string Headline()
        {
            return Outcome switch
            {
                MatchOutcome.Win => "You won the match. Your stat summary is below.",
                MatchOutcome.Loss => "You lost this match. Your stat summary is below.",
                MatchOutcome.Draw => "The match ended even. Your stat summary is below.",
                _ => "The match is still in progress.",
            };
        }
    }
}
