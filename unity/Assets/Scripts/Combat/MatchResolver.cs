namespace Keepfall.Combat
{
    /// <summary>The decided outcome of a match from the player's perspective (source-of-truth §4).</summary>
    public enum MatchOutcome
    {
        /// <summary>Match still in progress (clock running, neither win condition met).</summary>
        Undecided = 0,

        /// <summary>Player won: destroyed 2 of 3 enemy towers, or led on damage at 3:00 (§4).</summary>
        Win = 1,

        /// <summary>Player lost: the AI destroyed 2 of 3 player towers, or led on damage at 3:00.</summary>
        Loss = 2,

        /// <summary>Equal damage at 3:00 with neither side reaching 2 towers — a true draw.</summary>
        Draw = 3,
    }

    /// <summary>
    /// Pure win-condition evaluator (source-of-truth §4): a player WINS by destroying 2 of 3
    /// enemy towers, OR by holding the most tower damage when the 3:00 clock expires. Symmetric
    /// for the AI side. Fully deterministic — it only reads <see cref="MatchState"/> — so the
    /// same seed and same play sequence always resolve to the same outcome, which is what makes
    /// retry replay honest (§6 Product 3). No UnityEngine dependency; EditMode-testable.
    /// </summary>
    public static class MatchResolver
    {
        /// <summary>
        /// Resolves the current outcome. Decisive tower destruction (2 of 3) ends the match
        /// immediately and takes precedence over the clock; if both sides somehow reach 2 in the
        /// same evaluation, the side that destroyed MORE wins, falling through to the damage
        /// tiebreak on an exact tie. While the clock is still running and neither side has 2
        /// towers down, the match is <see cref="MatchOutcome.Undecided"/>. At/after 3:00 the
        /// most-damage rule decides, with an exact damage tie reported as a draw.
        /// </summary>
        public static MatchOutcome Resolve(MatchState state)
        {
            int enemyDown = state.DestroyedEnemyTowers;   // enemy towers the PLAYER destroyed
            int playerDown = state.DestroyedPlayerTowers; // player towers the ENEMY destroyed

            bool playerDecisive = enemyDown >= MatchState.TowersToWin;
            bool enemyDecisive = playerDown >= MatchState.TowersToWin;

            // Decisive destruction ends the match regardless of the clock (§4).
            if (playerDecisive || enemyDecisive)
            {
                if (playerDecisive && enemyDecisive)
                {
                    // Both at/over the threshold in the same tick: more destroyed wins.
                    if (enemyDown > playerDown)
                    {
                        return MatchOutcome.Win;
                    }

                    if (playerDown > enemyDown)
                    {
                        return MatchOutcome.Loss;
                    }

                    return ResolveByDamage(state); // equal towers → fall to damage tiebreak.
                }

                return playerDecisive ? MatchOutcome.Win : MatchOutcome.Loss;
            }

            // Not decisive yet: while time remains, the match is open.
            if (!state.TimeExpired)
            {
                return MatchOutcome.Undecided;
            }

            // Clock expired without 2 towers down on either side → most-damage rule (§4).
            return ResolveByDamage(state);
        }

        private static MatchOutcome ResolveByDamage(MatchState state)
        {
            int playerDamage = state.DamageDealtToEnemy; // damage PLAYER dealt
            int enemyDamage = state.DamageDealtToPlayer; // damage ENEMY dealt

            if (playerDamage > enemyDamage)
            {
                return MatchOutcome.Win;
            }

            if (enemyDamage > playerDamage)
            {
                return MatchOutcome.Loss;
            }

            return MatchOutcome.Draw;
        }
    }
}
