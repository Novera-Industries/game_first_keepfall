namespace Keepfall.Data
{
    /// <summary>
    /// Plain, immutable seed row for one roster unit. Lets the canonical 24-unit roster live as
    /// a static array (see <see cref="RosterCatalog"/>) that both the editor asset-importer and
    /// EditMode tests consume without Unity ScriptableObject plumbing. Mirrors
    /// <see cref="UnitDefinition"/> field-for-field.
    /// </summary>
    public readonly struct UnitSeed
    {
        /// <summary>Stable unique id.</summary>
        public readonly string Id;

        /// <summary>Player-facing display name.</summary>
        public readonly string DisplayName;

        /// <summary>Battlefield role (§3).</summary>
        public readonly Role Role;

        /// <summary>Unlock tier within the 6/10/6/2 ladder (§2).</summary>
        public readonly UnlockTier UnlockTier;

        /// <summary>In-match elixir cost, 1–10 (§4/§5).</summary>
        public readonly int ElixirCost;

        /// <summary>Stone unlock cost (§2 ladder). Always Stone, never Shards (§10.2).</summary>
        public readonly int StoneCost;

        /// <summary>Base combat stats (§4).</summary>
        public readonly UnitStats Stats;

        /// <summary>True for the 2 lateral Master units (§3).</summary>
        public readonly bool IsLateralMaster;

        /// <summary>Creates a seed row.</summary>
        public UnitSeed(
            string id,
            string displayName,
            Role role,
            UnlockTier unlockTier,
            int elixirCost,
            int stoneCost,
            UnitStats stats,
            bool isLateralMaster)
        {
            Id = id;
            DisplayName = displayName;
            Role = role;
            UnlockTier = unlockTier;
            ElixirCost = elixirCost;
            StoneCost = stoneCost;
            Stats = stats;
            IsLateralMaster = isLateralMaster;
        }
    }
}
