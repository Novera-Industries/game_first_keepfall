using System.Collections.Generic;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Locks the canonical Shard IAP ladder (source-of-truth §7) against
    /// <see cref="IapCatalog"/>: exactly five packs, ids + shards + USD match the canonical
    /// table, the effective $/Shard strictly improves with size (honest bulk convenience,
    /// never an outcome — §6/§10), the starter is the $0.99 / 100-Shard D3 offer, and every
    /// product id is under the <c>com.vyradata.keepfall.shards.</c> prefix.
    /// </summary>
    public sealed class IapCatalogTests
    {
        private const string ShardPrefix = "com.vyradata.keepfall.shards.";

        // The canonical table from source-of-truth §7 / config/iap-catalog.json.
        private static readonly (string productId, int shards, decimal usd)[] Canonical =
        {
            ("com.vyradata.keepfall.shards.starter", 100, 0.99m),
            ("com.vyradata.keepfall.shards.pouch", 550, 4.99m),
            ("com.vyradata.keepfall.shards.chest", 1200, 9.99m),
            ("com.vyradata.keepfall.shards.vault", 2600, 19.99m),
            ("com.vyradata.keepfall.shards.hoard", 7000, 49.99m),
        };

        [Test]
        public void ShardPacks_AreExactlyFive()
        {
            Assert.AreEqual(5, IapCatalog.ShardPacks.Count, "Exactly five Shard packs (§7).");
        }

        [Test]
        public void ShardPacks_MatchTheCanonicalIdsAndAmounts()
        {
            IReadOnlyList<ShopSku> packs = IapCatalog.ShardPacks;
            Assert.AreEqual(Canonical.Length, packs.Count);

            for (int i = 0; i < Canonical.Length; i++)
            {
                Assert.AreEqual(Canonical[i].productId, packs[i].StoreKitProductId,
                    $"Pack {i} product id must match the canonical ladder.");
                Assert.AreEqual(Canonical[i].shards, packs[i].ShardsGranted,
                    $"Pack {i} ({packs[i].StoreKitProductId}) Shard amount must match the catalog.");

                bool hasUsd = IapCatalog.UsdByProductId.TryGetValue(
                    packs[i].StoreKitProductId, out decimal usd);
                Assert.IsTrue(hasUsd, $"Pack {packs[i].StoreKitProductId} must have a USD price.");
                Assert.AreEqual(Canonical[i].usd, usd,
                    $"Pack {packs[i].StoreKitProductId} USD must match the catalog.");
            }
        }

        [Test]
        public void DollarPerShard_StrictlyDecreasesAsUsdIncreases()
        {
            IReadOnlyList<ShopSku> packs = IapCatalog.ShardPacks;

            decimal previousPerShard = decimal.MaxValue;
            decimal previousUsd = -1m;
            foreach (ShopSku pack in packs)
            {
                decimal usd = IapCatalog.UsdByProductId[pack.StoreKitProductId];
                Assert.Greater(usd, previousUsd, "Packs must be ordered by ascending USD.");

                decimal perShard = usd / pack.ShardsGranted;
                Assert.Less(perShard, previousPerShard,
                    $"$/Shard must strictly improve at {pack.StoreKitProductId} (better value at higher tiers, §7).");

                previousPerShard = perShard;
                previousUsd = usd;
            }
        }

        [Test]
        public void Starter_IsTheD3Offer_At099For100Shards()
        {
            ShopSku starter = IapCatalog.StarterPack;
            Assert.AreEqual("com.vyradata.keepfall.shards.starter", starter.StoreKitProductId);
            Assert.AreEqual(100, starter.ShardsGranted, "Starter grants 100 Shards (§7).");
            Assert.AreEqual(0.99m, IapCatalog.UsdByProductId[starter.StoreKitProductId],
                "Starter is the $0.99 D3 offer (§7/§8).");

            // The starter must also be the first entry in the ladder.
            Assert.AreEqual(starter.StoreKitProductId, IapCatalog.ShardPacks[0].StoreKitProductId);
        }

        [Test]
        public void EveryShardProductId_UsesTheShardPrefix()
        {
            foreach (ShopSku pack in IapCatalog.ShardPacks)
            {
                StringAssert.StartsWith(ShardPrefix, pack.StoreKitProductId,
                    "Shard pack product ids live under the canonical prefix (§7).");
            }
        }

        [Test]
        public void PlusProductId_IsTheSingleSubscriptionTier()
        {
            Assert.AreEqual("com.vyradata.keepfall.plus.monthly", IapCatalog.PlusProductId,
                "Keepfall Plus is the one subscription tier (§6).");
        }
    }
}
