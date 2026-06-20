using System;

namespace Keepfall.Data
{
    /// <summary>
    /// Base combat stats for a unit (source-of-truth §4 combat, §3 roster). These are the
    /// pre-synergy baseline values the combat simulation seeds from. Power in Keepfall comes
    /// from synergy and positioning, NOT from raw stat inflation (§3), so the spread between the
    /// weakest and strongest unit in any single stat is intentionally narrow.
    /// <para>
    /// Plain serializable struct so <see cref="UnitDefinition"/> ScriptableObjects and the
    /// static seed catalog share one shape, and so EditMode tests can construct stats without
    /// Unity asset plumbing.
    /// </para>
    /// </summary>
    [Serializable]
    public struct UnitStats : IEquatable<UnitStats>
    {
        /// <summary>Hit points. Vanguards sit highest; ranged/control units lowest.</summary>
        public int Hp;

        /// <summary>Damage per hit (pre-synergy). Splash units trade single-target for area.</summary>
        public int Dmg;

        /// <summary>Attack/aim range in lane units. Melee ≈ 1; Archer/Mage reach further.</summary>
        public float Range;

        /// <summary>Move speed in lane units per second. Skirmishers fastest; Engineers static.</summary>
        public float Speed;

        /// <summary>Creates a stat block.</summary>
        public UnitStats(int hp, int dmg, float range, float speed)
        {
            Hp = hp;
            Dmg = dmg;
            Range = range;
            Speed = speed;
        }

        /// <inheritdoc />
        public bool Equals(UnitStats other) =>
            Hp == other.Hp &&
            Dmg == other.Dmg &&
            Math.Abs(Range - other.Range) < 1e-4f &&
            Math.Abs(Speed - other.Speed) < 1e-4f;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is UnitStats s && Equals(s);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Hp, Dmg, Range, Speed);

        /// <inheritdoc />
        public override string ToString() => $"hp={Hp} dmg={Dmg} rng={Range:0.0} spd={Speed:0.0}";
    }
}
