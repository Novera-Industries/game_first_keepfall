namespace Keepfall.Data
{
    /// <summary>
    /// The six unit roles (source-of-truth §3). Every role hosts exactly one Starter and one
    /// Specialist unit; the ten Core units are distributed across roles; the two Master units
    /// are cross-role lateral options. Roles define a unit's battlefield function, never a
    /// power ceiling — depth comes from how roles combine (a Mage behind a Vanguard), not from
    /// any single role dominating (§3 "synergy is the depth engine").
    /// </summary>
    public enum Role
    {
        /// <summary>Tank, frontline damage absorption. Art accent: deep blue + steel.</summary>
        Vanguard = 0,

        /// <summary>Mobility, flanking, harassment. Art accent: amber + leather brown.</summary>
        Skirmisher = 1,

        /// <summary>Ranged single-target damage. Art accent: forest green + bone.</summary>
        Archer = 2,

        /// <summary>Area-of-effect, splash, control. Art accent: violet + ember orange.</summary>
        Mage = 3,

        /// <summary>Buildings, traps, defensive control. Art accent: slate grey + copper.</summary>
        Engineer = 4,

        /// <summary>Heavy hitter, win-condition unit. Art accent: crimson + gold.</summary>
        Champion = 5,
    }
}
