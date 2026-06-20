using System;

namespace Keepfall.Combat
{
    /// <summary>Which side a tower or actor belongs to. Phase 1 is single-player PvE (§0): the
    /// <see cref="Enemy"/> side is always the AI, never another human (PvP is Phase 2, inert).</summary>
    public enum Side
    {
        Player = 0,
        Enemy = 1,
    }

    /// <summary>
    /// A single tower (source-of-truth §4 — 3 towers per side). Holds hp and lane. A tower is
    /// destroyed when hp reaches 0; the win condition counts destroyed enemy towers.
    /// </summary>
    public sealed class Tower
    {
        /// <summary>Side this tower defends.</summary>
        public Side Side { get; }

        /// <summary>Lane (0..2) this tower stands in.</summary>
        public int Lane { get; }

        /// <summary>Max hit points.</summary>
        public int MaxHp { get; }

        /// <summary>Current hit points (0 = destroyed).</summary>
        public int Hp { get; private set; }

        /// <summary>True once hp has reached 0.</summary>
        public bool IsDestroyed => Hp <= 0;

        /// <summary>Cumulative damage this tower has taken (for the most-damage tiebreak, §4).</summary>
        public int DamageTaken => MaxHp - Hp;

        /// <summary>Creates a tower at full hp.</summary>
        public Tower(Side side, int lane, int maxHp)
        {
            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            }

            Side = side;
            Lane = lane;
            MaxHp = maxHp;
            Hp = maxHp;
        }

        /// <summary>Applies non-negative damage, clamping hp at 0. Returns damage actually dealt.</summary>
        public int ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDestroyed)
            {
                return 0;
            }

            int dealt = Math.Min(amount, Hp);
            Hp -= dealt;
            return dealt;
        }
    }

    /// <summary>
    /// Authoritative in-memory state of one PvE match (source-of-truth §4): three towers per
    /// side, a 3:00 (180s) clock, and the shared deterministic seed that drives the hand, the AI,
    /// and any map randomization so a retry replays identically (§6 Product 3). Pure C# — no
    /// UnityEngine — so the whole simulation is EditMode-testable.
    /// </summary>
    public sealed class MatchState
    {
        /// <summary>Towers per side (§4).</summary>
        public const int TowersPerSide = 3;

        /// <summary>Towers that must fall for a decisive win (§4: "destroy 2 of 3").</summary>
        public const int TowersToWin = 2;

        /// <summary>Match length in seconds (§4: hold most damage at 3:00).</summary>
        public const double MatchDurationSeconds = 180.0;

        /// <summary>Default tower hp. Tunable via RemoteConfig at the bootstrap layer.</summary>
        public const int DefaultTowerHp = 1400;

        /// <summary>The seed every deterministic sub-system derives from (§6 Product 3).</summary>
        public ulong MatchSeed { get; }

        /// <summary>The player's three towers, indexed by lane.</summary>
        public Tower[] PlayerTowers { get; }

        /// <summary>The enemy (AI) three towers, indexed by lane.</summary>
        public Tower[] EnemyTowers { get; }

        /// <summary>Elapsed match time in seconds, advanced by <see cref="Tick"/>.</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>True once the clock has reached <see cref="MatchDurationSeconds"/>.</summary>
        public bool TimeExpired => ElapsedSeconds >= MatchDurationSeconds;

        /// <summary>Creates a fresh match. Same seed + same inputs → identical playthrough.</summary>
        public MatchState(ulong matchSeed, int towerHp = DefaultTowerHp)
        {
            MatchSeed = matchSeed;
            PlayerTowers = new Tower[TowersPerSide];
            EnemyTowers = new Tower[TowersPerSide];
            for (int lane = 0; lane < TowersPerSide; lane++)
            {
                PlayerTowers[lane] = new Tower(Side.Player, lane, towerHp);
                EnemyTowers[lane] = new Tower(Side.Enemy, lane, towerHp);
            }
        }

        /// <summary>Advances the match clock (clamped to the 180s duration). Negative ignored.</summary>
        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds <= 0)
            {
                return;
            }

            ElapsedSeconds += deltaSeconds;
            if (ElapsedSeconds > MatchDurationSeconds)
            {
                ElapsedSeconds = MatchDurationSeconds;
            }
        }

        /// <summary>Count of an enemy side's destroyed towers (for the win check).</summary>
        public int DestroyedEnemyTowers => CountDestroyed(EnemyTowers);

        /// <summary>Count of the player's destroyed towers (for the loss check).</summary>
        public int DestroyedPlayerTowers => CountDestroyed(PlayerTowers);

        /// <summary>Total damage the player has dealt to enemy towers (tiebreak, §4).</summary>
        public int DamageDealtToEnemy => SumDamage(EnemyTowers);

        /// <summary>Total damage the enemy has dealt to player towers (tiebreak, §4).</summary>
        public int DamageDealtToPlayer => SumDamage(PlayerTowers);

        private static int CountDestroyed(Tower[] towers)
        {
            int n = 0;
            foreach (Tower t in towers)
            {
                if (t.IsDestroyed)
                {
                    n++;
                }
            }

            return n;
        }

        private static int SumDamage(Tower[] towers)
        {
            int n = 0;
            foreach (Tower t in towers)
            {
                n += t.DamageTaken;
            }

            return n;
        }
    }
}
