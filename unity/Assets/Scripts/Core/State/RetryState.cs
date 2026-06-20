using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Client-side cache for PvE retry tokens (source-of-truth §6 Product 3). The Cloudflare
    /// Worker is the AUTHORITY — the rules "cannot retry a win", "cannot retry a retry", and
    /// "rewards capped at first-attempt rate" are enforced server-side, not here. This DTO
    /// mirrors token balance and loss streaks so the client can show offers (only after 3
    /// consecutive losses on the SAME match, never on first loss — §8) and request grants;
    /// the server validates every redeem.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RetryState
    {
        /// <summary>Locally-known token balance. Server is authoritative on redeem/grant.</summary>
        [JsonProperty("tokenCount")]
        public int TokenCount;

        /// <summary>UTC of the last successful daily grant. The server caps daily grants
        /// (1/day, cap 3; Plus +1/day, cap 5); the client uses this only to avoid spamming
        /// the grant endpoint.</summary>
        [JsonProperty("lastDailyGrantUtc")]
        public DateTimeOffset LastDailyGrantUtc;

        /// <summary>Consecutive losses per match id. Drives the "offer only after 3 losses on
        /// the same match" gate (§8). Reset to 0 for a match on win or retry success.</summary>
        [JsonProperty("perMatchLossStreak")]
        public Dictionary<string, int> PerMatchLossStreak = new Dictionary<string, int>();

        /// <summary>Required by the serializer.</summary>
        public RetryState()
        {
        }
    }
}
