using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Save state for unit ownership (source-of-truth §2–§3). 24 units total; all unlocked
    /// with Stone (soft currency) — no unit is ever gated by money (§10.2). The Stone-spent
    /// ledger records what was paid per unit, for analytics and for an honest re-derivation
    /// of progression. Behaviour (unlock cost lookup, validation) lives in the Economy
    /// assembly; this is the data holder only.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RosterState
    {
        /// <summary>Ids of units the player owns. Starter units are seeded here at install.</summary>
        [JsonProperty("unlockedUnitIds")]
        public List<string> UnlockedUnitIds = new List<string>();

        /// <summary>Per-unit Stone spent to unlock (unitId -> Stone). Always Stone, never
        /// Shards — keeps the "units are earned, not bought" invariant auditable.</summary>
        [JsonProperty("stoneSpentLedger")]
        public Dictionary<string, long> StoneSpentLedger = new Dictionary<string, long>();

        /// <summary>Required by the serializer.</summary>
        public RosterState()
        {
        }
    }
}
