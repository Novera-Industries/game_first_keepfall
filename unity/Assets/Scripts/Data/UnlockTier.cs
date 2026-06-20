namespace Keepfall.Data
{
    /// <summary>
    /// Unit unlock tiers (source-of-truth §2 unlock cost ladder, §3 roster). The 24-unit roster
    /// splits EXACTLY 6 / 10 / 6 / 2 across these tiers. Tier governs Stone cost and expected
    /// unlock day for an F2P player — it NEVER governs combat power. A Master is not a strict
    /// upgrade over a Starter; it is a sideways option (see <see cref="UnitDefinition"/>).
    /// </summary>
    public enum UnlockTier
    {
        /// <summary>6 units. Free or 50–150 Stone. Expected unlock day 1–3. Seeded at install.</summary>
        Starter = 0,

        /// <summary>10 units. 300–1,200 Stone. Expected unlock day 4–14.</summary>
        Core = 1,

        /// <summary>6 units. 2,500–6,000 Stone. Expected unlock day 12–25.</summary>
        Specialist = 2,

        /// <summary>2 units. 10,000–15,000 Stone. Expected unlock day 25–40. Lateral, not stronger.</summary>
        Master = 3,
    }
}
