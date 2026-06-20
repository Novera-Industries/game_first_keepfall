namespace Keepfall.Funnel
{
    /// <summary>
    /// Where a funnel trigger may surface (source-of-truth §8 "Where" column, taxonomy §6
    /// "Placement"). This enum is deliberately a CLOSED set: there is <b>no</b> <c>AppOpen</c>
    /// value, because no trigger may fire on app open (SoT §10.5, §10.6; taxonomy §8 audit
    /// hook "No app-open selling"). A trigger that cannot name a placement here cannot be shown.
    /// </summary>
    public enum FunnelPlacement
    {
        /// <summary>The tile screen / tile UI (accelerator discoverability + hints, SoT §8 D2, D11).</summary>
        TileScreen = 0,

        /// <summary>The Shop tab, as a single inline banner (SoT §8 D3, D7).</summary>
        ShopTab = 1,

        /// <summary>The post-match loss screen / its footer (SoT §8 D4–D6, D15–D21).</summary>
        LossScreen = 2,

        /// <summary>The Battle Pass tab (SoT §8 D8–D10, D22–D28).</summary>
        PassTab = 3,

        /// <summary>The player profile screen (SoT §8 D14, D22–D28, D29–D30).</summary>
        Profile = 4,
    }
}
