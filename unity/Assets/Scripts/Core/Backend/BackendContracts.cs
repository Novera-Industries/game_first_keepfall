using Newtonsoft.Json;

namespace Keepfall.Core.Backend
{
    /// <summary>
    /// Request/response DTOs for the Cloudflare Worker API. These define the WIRE CONTRACT the
    /// client and the backend agent share; field names are the JSON the Worker reads/writes.
    /// The Worker is the AUTHORITY for receipt validation and all retry-token rules
    /// (source-of-truth §6 Product 3) — the client never decides these outcomes.
    /// </summary>
    public static class BackendContracts
    {
    }

    /// <summary>Response from <c>GET /v1/save</c>: the player's last cloud save blob.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CloudSavePullResponse
    {
        /// <summary>Serialized <c>PlayerState</c> JSON, or null if no cloud save exists.</summary>
        [JsonProperty("playerStateJson")]
        public string PlayerStateJson;

        /// <summary>Server-side version/etag for conflict detection on the next push.</summary>
        [JsonProperty("version")]
        public long Version;
    }

    /// <summary>Request body for <c>POST /v1/save</c>.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CloudSavePushRequest
    {
        /// <summary>Serialized <c>PlayerState</c> JSON to persist.</summary>
        [JsonProperty("playerStateJson")]
        public string PlayerStateJson;

        /// <summary>Last known server version, for optimistic concurrency.</summary>
        [JsonProperty("baseVersion")]
        public long BaseVersion;
    }

    /// <summary>Response from <c>POST /v1/save</c>.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CloudSavePushResponse
    {
        /// <summary>True if the push was accepted.</summary>
        [JsonProperty("accepted")]
        public bool Accepted;

        /// <summary>New authoritative version after a successful push.</summary>
        [JsonProperty("version")]
        public long Version;
    }

    /// <summary>Request body for <c>POST /v1/receipts/validate</c> (StoreKit 2).</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ValidateReceiptRequest
    {
        /// <summary>StoreKit 2 signed transaction (JWS) to validate server-side.</summary>
        [JsonProperty("signedTransaction")]
        public string SignedTransaction;

        /// <summary>Product id the client believes was purchased (cross-checked server-side).</summary>
        [JsonProperty("productId")]
        public string ProductId;
    }

    /// <summary>Response from <c>POST /v1/receipts/validate</c>.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ValidateReceiptResponse
    {
        /// <summary>True only if the Worker verified the receipt with Apple.</summary>
        [JsonProperty("valid")]
        public bool Valid;

        /// <summary>Validated product id (authoritative).</summary>
        [JsonProperty("productId")]
        public string ProductId;

        /// <summary>For subscriptions: validated period-end (UTC, ISO-8601), else null.</summary>
        [JsonProperty("currentPeriodEndUtc")]
        public string CurrentPeriodEndUtc;
    }

    /// <summary>Request body for <c>POST /v1/retry/request</c>: ask whether a match is retryable.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RequestRetryTokenRequest
    {
        /// <summary>Server-issued attempt id whose retry eligibility is being checked.</summary>
        [JsonProperty("attemptId")]
        public string AttemptId;
    }

    /// <summary>
    /// Response describing retry eligibility. The Worker enforces "cannot retry a win" and
    /// "cannot retry a retry" (§6) — the client only renders what the server returns.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RequestRetryTokenResponse
    {
        /// <summary>True if this match may be retried with a token.</summary>
        [JsonProperty("eligible")]
        public bool Eligible;

        /// <summary>Reason code when not eligible (e.g. "match_was_won", "already_a_retry").</summary>
        [JsonProperty("reason")]
        public string Reason;

        /// <summary>Server-known retry-token balance.</summary>
        [JsonProperty("tokenBalance")]
        public int TokenBalance;
    }

    /// <summary>Request body for <c>POST /v1/retry/redeem</c>.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RedeemRetryTokenRequest
    {
        /// <summary>Attempt id to redeem a token against.</summary>
        [JsonProperty("attemptId")]
        public string AttemptId;
    }

    /// <summary>
    /// Response from <c>POST /v1/retry/redeem</c>. On success the Worker returns the EXACT
    /// replay seed (identical AI, map seed, starting hand — §6) and flags the rematch so its
    /// rewards are capped at the first-attempt rate (enforced server-side).
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RedeemRetryTokenResponse
    {
        /// <summary>True if a token was spent and a retry was authorized.</summary>
        [JsonProperty("redeemed")]
        public bool Redeemed;

        /// <summary>Reason code when redemption was refused.</summary>
        [JsonProperty("reason")]
        public string Reason;

        /// <summary>Deterministic seed reproducing the original match (AI/map/hand).</summary>
        [JsonProperty("replaySeed")]
        public string ReplaySeed;

        /// <summary>Remaining token balance after redemption.</summary>
        [JsonProperty("tokenBalance")]
        public int TokenBalance;

        /// <summary>True for the retried attempt so the client UI shows capped rewards.</summary>
        [JsonProperty("rewardsCappedToFirstAttempt")]
        public bool RewardsCappedToFirstAttempt;
    }

    /// <summary>Response from <c>POST /v1/retry/grant-daily</c>.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class GrantDailyRetryTokenResponse
    {
        /// <summary>True if a daily token was granted this call (server enforces the cap).</summary>
        [JsonProperty("granted")]
        public bool Granted;

        /// <summary>Token balance after the grant attempt.</summary>
        [JsonProperty("tokenBalance")]
        public int TokenBalance;

        /// <summary>UTC (ISO-8601) when the next daily grant becomes available.</summary>
        [JsonProperty("nextGrantUtc")]
        public string NextGrantUtc;
    }
}
