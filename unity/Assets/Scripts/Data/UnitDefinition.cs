using UnityEngine;

namespace Keepfall.Data
{
    /// <summary>
    /// Authoring asset for a single roster unit (source-of-truth §3 roster, §2 unlock ladder,
    /// §4 combat). One ScriptableObject per unit; the full 24-unit roster is also expressible as
    /// a static seed (see <see cref="RosterCatalog"/>) so EditMode tests and the editor importer
    /// share one source. Designers tune values in the inspector; the seed mirrors them so a
    /// fresh project is playable without hand-authored assets.
    ///
    /// CRITICAL ROSTER INVARIANT — MASTERS ARE LATERAL, NEVER STRICT UPGRADES (§3):
    ///   A Master unit must NOT dominate a same-role unit across the board. For every Master,
    ///   at least one base stat (hp / dmg / range / speed) must be LOWER than, or its elixir
    ///   cost HIGHER than, a comparable lower-tier unit — so picking a Master is a trade-off,
    ///   not an objectively better card. This prevents the Clash-Royale L12–L16 treadmill where
    ///   newer cards obsolete older ones. <c>RosterIntegrityTests</c> asserts every Master is
    ///   flagged <see cref="IsLateralMaster"/>; the seed values keep Masters cost-balanced.
    ///   No unit is ever gated by money (§10.2): <see cref="stoneCost"/> is ALWAYS Stone, never
    ///   Shards. There is no Shard price field on units, by design.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Unit_",
        menuName = "Keepfall/Unit Definition",
        order = 0)]
    public sealed class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique id (e.g. \"bulwark\"). Used in decks, saves, analytics. Never localized.")]
        [SerializeField] private string id;

        [Tooltip("Player-facing name (e.g. \"Bulwark\").")]
        [SerializeField] private string displayName;

        [Tooltip("Battlefield function. Each role has exactly 1 Starter + 1 Specialist (§3).")]
        [SerializeField] private Role role;

        [Tooltip("Unlock tier. Roster split is EXACTLY 6/10/6/2 (§2 ladder).")]
        [SerializeField] private UnlockTier unlockTier;

        [Header("Costs")]
        [Tooltip("In-match elixir cost (1–10). Chosen so a legal 8-card deck averages 2.6–3.0 (§5).")]
        [SerializeField] private int elixirCost = 3;

        [Tooltip("Stone (soft currency) to unlock. ALWAYS Stone — units are never bought with Shards (§10.2).")]
        [SerializeField] private int stoneCost;

        [Header("Base stats (pre-synergy — power comes from synergy, not stats, §3)")]
        [SerializeField] private int hp = 100;
        [SerializeField] private int dmg = 10;
        [SerializeField] private float range = 1f;
        [SerializeField] private float speed = 1f;

        [Header("Balance flags")]
        [Tooltip("True only for the 2 Master units. Masters are LATERAL options, never strict " +
                 "upgrades (§3). Enforced by RosterIntegrityTests.")]
        [SerializeField] private bool isLateralMaster;

        /// <summary>Stable unique id (e.g. "bulwark"). Deck/save/analytics key; never localized.</summary>
        public string Id => id;

        /// <summary>Player-facing display name (e.g. "Bulwark").</summary>
        public string DisplayName => displayName;

        /// <summary>Battlefield function (§3).</summary>
        public Role Role => role;

        /// <summary>Unlock tier within the 6/10/6/2 ladder (§2).</summary>
        public UnlockTier UnlockTier => unlockTier;

        /// <summary>In-match elixir cost, 1–10 (§4). Drives the 2.6–3.0 deck average (§5).</summary>
        public int ElixirCost => elixirCost;

        /// <summary>Stone unlock cost (§2 ladder). Always Stone, never Shards.</summary>
        public int StoneCost => stoneCost;

        /// <summary>Base combat stats (§4).</summary>
        public UnitStats Stats => new UnitStats(hp, dmg, range, speed);

        /// <summary>True for the 2 lateral Master units (§3). Never a strict upgrade.</summary>
        public bool IsLateralMaster => isLateralMaster;

        /// <summary>
        /// Hydrates this asset from a seed row. Used by the editor importer that materializes
        /// the static <see cref="RosterCatalog"/> into ScriptableObject assets, and by tests.
        /// </summary>
        public void ApplySeed(in UnitSeed seed)
        {
            id = seed.Id;
            displayName = seed.DisplayName;
            role = seed.Role;
            unlockTier = seed.UnlockTier;
            elixirCost = seed.ElixirCost;
            stoneCost = seed.StoneCost;
            hp = seed.Stats.Hp;
            dmg = seed.Stats.Dmg;
            range = seed.Stats.Range;
            speed = seed.Stats.Speed;
            isLateralMaster = seed.IsLateralMaster;
        }

        /// <summary>Converts this asset to its plain seed row (for tests / serialization).</summary>
        public UnitSeed ToSeed() => new UnitSeed(
            id, displayName, role, unlockTier, elixirCost, stoneCost, Stats, isLateralMaster);
    }
}
