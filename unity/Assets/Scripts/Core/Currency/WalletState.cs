using Newtonsoft.Json;

namespace Keepfall.Core.Currency
{
    /// <summary>
    /// Serializable balances DTO. Plain data — the <see cref="Wallet"/> service owns all
    /// mutation and guarding. Two longs only, mirroring the two-currency contract (§1).
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class WalletState
    {
        /// <summary>Stone balance (soft currency). Never negative.</summary>
        [JsonProperty("stone")]
        public long Stone;

        /// <summary>Shards balance (premium currency). Never negative.</summary>
        [JsonProperty("shards")]
        public long Shards;

        /// <summary>New empty wallet (0 Stone, 0 Shards).</summary>
        public WalletState()
        {
        }

        /// <summary>Seeded wallet, primarily for tests and migrations.</summary>
        public WalletState(long stone, long shards)
        {
            Stone = stone;
            Shards = shards;
        }
    }
}
