using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Tile rank tiers (source-of-truth §2). Yield rate and pre-claim cap scale by rank;
    /// the actual numbers live in remote config (keys <c>tile.yield.t1/t2/t3</c>) so they
    /// can be tuned without a rebuild. Serialized by name for save-file readability.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TileRank
    {
        /// <summary>Early tile. Canonical default 10 Stone/hour, 120 cap.</summary>
        T1 = 1,

        /// <summary>Mid tile. Canonical default 25 Stone/hour, 300 cap.</summary>
        T2 = 2,

        /// <summary>Late Phase 1 tile. Canonical default 60 Stone/hour, 720 cap.</summary>
        T3 = 3,
    }
}
