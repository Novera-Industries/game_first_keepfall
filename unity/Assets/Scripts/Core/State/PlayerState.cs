using System.Collections.Generic;
using Keepfall.Core.Currency;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// The complete, serializable player save. Aggregates every sub-state plus the wallet and
    /// a <see cref="SchemaVersion"/> for migration. This is the single object the
    /// <c>SaveSystem</c> reads/writes and the backend cloud-save endpoint round-trips. Feature
    /// assemblies write behaviour that operates on these holders; the holders themselves stay
    /// plain data.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PlayerState
    {
        /// <summary>Current save-format version. Bump when a field's meaning changes; the
        /// <c>SaveSystem</c> runs migrations from older versions up to this one.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Schema version this instance was written with.</summary>
        [JsonProperty("schemaVersion")]
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Wallet balances (the two canonical currencies).</summary>
        [JsonProperty("wallet")]
        public WalletState Wallet = new WalletState();

        /// <summary>Owned tiles and their accrual state.</summary>
        [JsonProperty("tiles")]
        public List<TileState> Tiles = new List<TileState>();

        /// <summary>Unit ownership and Stone-spent ledger.</summary>
        [JsonProperty("roster")]
        public RosterState Roster = new RosterState();

        /// <summary>Decks and slot ownership.</summary>
        [JsonProperty("deck")]
        public DeckState Deck = new DeckState();

        /// <summary>Keepfall Plus subscription cache.</summary>
        [JsonProperty("subscription")]
        public SubscriptionState Subscription = new SubscriptionState();

        /// <summary>Conversion-funnel bookkeeping.</summary>
        [JsonProperty("funnel")]
        public FunnelState Funnel = new FunnelState();

        /// <summary>Retry-token cache (server is authoritative).</summary>
        [JsonProperty("retry")]
        public RetryState Retry = new RetryState();

        /// <summary>Permanently-owned cosmetics.</summary>
        [JsonProperty("cosmetics")]
        public CosmeticState Cosmetics = new CosmeticState();

        /// <summary>Creates a default-initialized state (all sub-states non-null).</summary>
        public PlayerState()
        {
        }
    }
}
