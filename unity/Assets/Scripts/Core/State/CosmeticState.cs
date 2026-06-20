using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Save state for owned cosmetics (source-of-truth §6–§7). Ownership is PERMANENT:
    /// cosmetics earned during a Keepfall Plus subscription are kept on cancellation (§6 hard
    /// exclusion). Nothing in the codebase may remove an id from this list. Cosmetics are
    /// visual-only — they confer no combat advantage (§6, §10).
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CosmeticState
    {
        /// <summary>Ids of permanently-owned cosmetics (skins, borders, etc.). Append-only.</summary>
        [JsonProperty("ownedCosmeticIds")]
        public List<string> OwnedCosmeticIds = new List<string>();

        /// <summary>Required by the serializer.</summary>
        public CosmeticState()
        {
        }
    }
}
