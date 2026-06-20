using System.Collections.Generic;
using System.Linq;

namespace Keepfall.Data
{
    /// <summary>
    /// The canonical Keepfall roster: EXACTLY 24 units = 6 Starter + 10 Core + 6 Specialist +
    /// 2 Master, across the 6 roles (source-of-truth §2 ladder, §3 roster). Each role has 1
    /// Starter and 1 Specialist; the 10 Core are distributed across roles; the 2 Master units
    /// are cross-role LATERAL options (never strict upgrades, §3).
    ///
    /// This static seed is the single source the editor importer materializes into
    /// <see cref="UnitDefinition"/> assets, and the source <c>RosterIntegrityTests</c> verifies.
    /// Elixir costs are chosen so a legal 8-card deck (avg 2.6–3.0, §5) is constructible from
    /// starter+core alone, keeping F2P viable. Stone costs follow the §2 ladder bands:
    /// Starter 0–150 · Core 300–1,200 · Specialist 2,500–6,000 · Master 10,000–15,000.
    /// Stats are deliberately narrow-spread because power comes from synergy, not stat inflation.
    ///
    /// ── FULL ROSTER (24) ─────────────────────────────────────────────────────────────────
    ///  #  Name            Role        Tier        Elx  Stone   Notes
    ///  1  Bulwark         Vanguard    Starter      4       0    seeded; wall tank
    ///  2  Hound           Skirmisher  Starter      2       0    seeded; fast flanker
    ///  3  Longshot        Archer      Starter      3      50    single-target sniper
    ///  4  Spark           Mage        Starter      3     100    small splash
    ///  5  Tower           Engineer    Starter      4     100    defensive building
    ///  6  Captain         Champion    Starter      4     150    starter win-condition
    ///  7  Rampart         Vanguard    Core         5     900    heavy shield tank
    ///  8  Outrider        Skirmisher  Core         2     400    raid harasser
    ///  9  Cutpurse        Skirmisher  Core         1     300    cheap cycle flanker
    /// 10  Marksman        Archer      Core         3     600    steady ranged DPS
    /// 11  Slinger         Archer      Core         2     450    cheap chip shooter
    /// 12  Frostweaver     Mage        Core         3     800    slow/control splash
    /// 13  Cinder          Mage        Core         2     500    cheap DoT splash
    /// 14  Bombard         Engineer    Core       4    1000    siege mortar building
    /// 15  Warden          Champion    Core       4    1100    bruiser win-condition
    /// 16  Reaver          Champion    Core       4    1200    aggressive win-condition
    /// 17  Standardbearer  Vanguard    Specialist   4    2500    buffs nearby allies
    /// 18  Pathfinder      Skirmisher  Specialist   2    2800    stealth flanker
    /// 19  Volley          Archer      Specialist   4    4000    multi-shot ranged
    /// 20  Wildfire        Mage        Specialist   4    4500    big AoE control
    /// 21  Snare           Engineer    Specialist   2    3200    trap / lane denial
    /// 22  Berserker       Champion    Specialist   5    6000    high-risk win-condition
    /// 23  Twinblade       Champion*   Master       4   12000    *lateral Skirmisher/Champion
    /// 24  Lodestone       Engineer*   Master       4   15000    *lateral Vanguard/Engineer
    /// ─────────────────────────────────────────────────────────────────────────────────────
    /// (* Master "role" is its primary slot; both play laterally across two roles by design.)
    /// </summary>
    public static class RosterCatalog
    {
        /// <summary>Authoritative total unit count (§2/§3).</summary>
        public const int TotalUnits = 24;

        /// <summary>Tier split — must sum to <see cref="TotalUnits"/> (§2 ladder).</summary>
        public const int StarterCount = 6;
        public const int CoreCount = 10;
        public const int SpecialistCount = 6;
        public const int MasterCount = 2;

        // Ids of the 6 starter units seeded into a fresh save (§8 D1: "3 tiles, 6 starters").
        // The Economy assembly reads these to populate RosterState.UnlockedUnitIds at install.
        public const string IdBulwark = "bulwark";
        public const string IdHound = "hound";
        public const string IdLongshot = "longshot";
        public const string IdSpark = "spark";
        public const string IdTower = "tower";
        public const string IdCaptain = "captain";

        /// <summary>
        /// The full 24-unit seed, mirroring the comment table above. Order is Starter → Core →
        /// Specialist → Master so the importer and tests can slice by tier deterministically.
        /// </summary>
        public static readonly IReadOnlyList<UnitSeed> Units = new[]
        {
            // ── 6 STARTER (1 per role) — Stone 0–150 ─────────────────────────────────────
            new UnitSeed(IdBulwark,  "Bulwark",        Role.Vanguard,   UnlockTier.Starter, 4,     0, new UnitStats(420,  18, 1.0f, 0.8f), false),
            new UnitSeed(IdHound,    "Hound",          Role.Skirmisher, UnlockTier.Starter, 2,     0, new UnitStats(160,  22, 1.0f, 1.8f), false),
            new UnitSeed(IdLongshot, "Longshot",       Role.Archer,     UnlockTier.Starter, 3,    50, new UnitStats(120,  40, 5.5f, 1.0f), false),
            new UnitSeed(IdSpark,    "Spark",          Role.Mage,       UnlockTier.Starter, 3,   100, new UnitStats(130,  30, 4.5f, 1.0f), false),
            new UnitSeed(IdTower,    "Tower",          Role.Engineer,   UnlockTier.Starter, 4,   100, new UnitStats(360,  28, 5.0f, 0.0f), false),
            new UnitSeed(IdCaptain,  "Captain",        Role.Champion,   UnlockTier.Starter, 4,   150, new UnitStats(300,  55, 1.2f, 1.1f), false),

            // ── 10 CORE (distributed) — Stone 300–1,200 ──────────────────────────────────
            new UnitSeed("rampart",     "Rampart",     Role.Vanguard,   UnlockTier.Core, 5,   900, new UnitStats(560,  20, 1.0f, 0.7f), false),
            new UnitSeed("outrider",    "Outrider",    Role.Skirmisher, UnlockTier.Core, 2,   400, new UnitStats(170,  24, 1.0f, 1.7f), false),
            new UnitSeed("cutpurse",    "Cutpurse",    Role.Skirmisher, UnlockTier.Core, 1,   300, new UnitStats(110,  16, 1.0f, 1.9f), false),
            new UnitSeed("marksman",    "Marksman",    Role.Archer,     UnlockTier.Core, 3,   600, new UnitStats(130,  42, 5.5f, 1.0f), false),
            new UnitSeed("slinger",     "Slinger",     Role.Archer,     UnlockTier.Core, 2,   450, new UnitStats(100,  28, 5.0f, 1.1f), false),
            new UnitSeed("frostweaver", "Frostweaver", Role.Mage,       UnlockTier.Core, 3,   800, new UnitStats(120,  26, 4.5f, 1.0f), false),
            new UnitSeed("cinder",      "Cinder",      Role.Mage,       UnlockTier.Core, 2,   500, new UnitStats(100,  22, 4.0f, 1.0f), false),
            new UnitSeed("bombard",     "Bombard",     Role.Engineer,   UnlockTier.Core, 4,  1000, new UnitStats(330,  46, 6.0f, 0.0f), false),
            new UnitSeed("warden",      "Warden",      Role.Champion,   UnlockTier.Core, 4,  1100, new UnitStats(340,  50, 1.2f, 1.0f), false),
            new UnitSeed("reaver",      "Reaver",      Role.Champion,   UnlockTier.Core, 4,  1200, new UnitStats(280,  62, 1.2f, 1.2f), false),

            // ── 6 SPECIALIST (1 per role) — Stone 2,500–6,000 ────────────────────────────
            new UnitSeed("standardbearer", "Standardbearer", Role.Vanguard,   UnlockTier.Specialist, 4, 2500, new UnitStats(400,  16, 1.0f, 0.9f), false),
            new UnitSeed("pathfinder",     "Pathfinder",     Role.Skirmisher, UnlockTier.Specialist, 2, 2800, new UnitStats(150,  26, 1.0f, 1.9f), false),
            new UnitSeed("volley",         "Volley",         Role.Archer,     UnlockTier.Specialist, 4, 4000, new UnitStats(140,  34, 5.5f, 1.0f), false),
            new UnitSeed("wildfire",       "Wildfire",       Role.Mage,       UnlockTier.Specialist, 4, 4500, new UnitStats(150,  38, 4.5f, 0.9f), false),
            new UnitSeed("snare",          "Snare",          Role.Engineer,   UnlockTier.Specialist, 2, 3200, new UnitStats(200,  10, 3.0f, 0.0f), false),
            new UnitSeed("berserker",      "Berserker",      Role.Champion,   UnlockTier.Specialist, 5, 6000, new UnitStats(360,  70, 1.2f, 1.3f), false),

            // ── 2 MASTER (cross-role, LATERAL — never strict upgrades, §3) — Stone 10k–15k ─
            // Twinblade: Skirmisher/Champion hybrid. Note dmg (52) < Berserker (70) and hp (260)
            // < Captain (300): it trades raw power for cross-role flexibility — a sideways pick.
            new UnitSeed("twinblade", "Twinblade", Role.Champion,  UnlockTier.Master, 4, 12000, new UnitStats(260, 52, 1.1f, 1.4f), true),
            // Lodestone: Vanguard/Engineer hybrid. Note hp (380) < Rampart (560) and dmg (24) is
            // modest: it pulls aggro like a tank AND anchors like a building, but tops neither.
            new UnitSeed("lodestone", "Lodestone", Role.Engineer,  UnlockTier.Master, 4, 15000, new UnitStats(380, 24, 2.0f, 0.5f), true),
        };

        /// <summary>Returns the seed for an id, or null if no such unit exists.</summary>
        public static UnitSeed? FindById(string id)
        {
            foreach (UnitSeed u in Units)
            {
                if (u.Id == id)
                {
                    return u;
                }
            }

            return null;
        }

        /// <summary>Ids of the 6 starter units seeded into a fresh save (§8 D1).</summary>
        public static IReadOnlyList<string> StarterUnitIds =>
            Units.Where(u => u.UnlockTier == UnlockTier.Starter).Select(u => u.Id).ToArray();

        /// <summary>All seeds of a given tier.</summary>
        public static IEnumerable<UnitSeed> OfTier(UnlockTier tier) =>
            Units.Where(u => u.UnlockTier == tier);

        /// <summary>All seeds of a given role.</summary>
        public static IEnumerable<UnitSeed> OfRole(Role role) =>
            Units.Where(u => u.Role == role);
    }
}
