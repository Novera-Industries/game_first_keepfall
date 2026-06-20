using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Save state for Keepfall Plus (source-of-truth §6 Product 2). EXACTLY ONE tier exists
    /// — multi-tier subscriptions are an anti-pattern (§10.8), so there is no "tier" field.
    /// Cosmetics granted during an active subscription are recorded here and KEPT FOREVER on
    /// cancellation (§6 hard exclusion) — the cancellation path must migrate
    /// <see cref="CosmeticsGrantedDuringSub"/> into <see cref="CosmeticState"/> and never
    /// revoke. Subscription is authoritative server-side via StoreKit 2 receipt validation;
    /// this client copy is a cache for UI/perk gating.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class SubscriptionState
    {
        /// <summary>Whether Plus is currently active (within paid or trial period).</summary>
        [JsonProperty("active")]
        public bool Active;

        /// <summary>StoreKit product id of the single Plus tier (e.g. "keepfall.plus.monthly").</summary>
        [JsonProperty("productId")]
        public string ProductId;

        /// <summary>True once the 7-day free trial has been consumed; a player may trial only
        /// once. Trial availability is also gated by the remote-config flag
        /// <c>plus.trial.enabled</c>.</summary>
        [JsonProperty("trialUsed")]
        public bool TrialUsed;

        /// <summary>UTC end of the current paid/trial period. Perks lapse after this unless
        /// renewed. Validated against the server; the client never extends it locally.</summary>
        [JsonProperty("currentPeriodEndUtc")]
        public DateTimeOffset CurrentPeriodEndUtc;

        /// <summary>Cosmetic ids granted while subscribed (monthly drops, etc.). Retained on
        /// cancellation — see class remarks. Distinct from <see cref="CosmeticState"/> until
        /// the keep-on-cancel migration folds them in.</summary>
        [JsonProperty("cosmeticsGrantedDuringSub")]
        public List<string> CosmeticsGrantedDuringSub = new List<string>();

        /// <summary>Required by the serializer.</summary>
        public SubscriptionState()
        {
        }
    }
}
