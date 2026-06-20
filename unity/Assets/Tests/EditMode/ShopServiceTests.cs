using System;
using System.Collections.Generic;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Covers the cosmetic Shop (source-of-truth §7): 14-day rotation surfaces 3–5 cosmetic
    /// SKUs, Shard packs are ALWAYS available (and never auto-presented — that is the funnel's
    /// job), Shard packs validate server-side before crediting, and the catalog is cosmetic-only.
    /// </summary>
    public sealed class ShopServiceTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        private static List<ShopSku> CosmeticPool()
        {
            var pool = new List<ShopSku>();
            for (int i = 0; i < 8; i++)
            {
                pool.Add(ShopSku.Cosmetic($"cos.{i}", $"skin.{i}", CurrencyType.Shards, 100 + i));
            }

            return pool;
        }

        private static List<ShopSku> ShardPacks()
        {
            return new List<ShopSku>
            {
                ShopSku.ShardPackSku("pack.099", "com.vyradata.keepfall.shards.starter", 100),
                ShopSku.ShardPackSku("pack.499", "com.vyradata.keepfall.shards.medium", 600),
            };
        }

        private static ShopService Build(
            out Wallet wallet, out CosmeticState cosmetics, out FakeBackendClient backend,
            out FakeTimeProvider clock, long shards = 0)
        {
            wallet = new Wallet(new WalletState(0, shards));
            cosmetics = new CosmeticState();
            backend = new FakeBackendClient();
            clock = new FakeTimeProvider(Now);
            return new ShopService(CosmeticPool(), ShardPacks(), wallet, cosmetics,
                new RemoteConfig(), backend, analytics: null, time: clock);
        }

        [Test]
        public void Rotation_SurfacesThreeToFiveCosmetics_AndIsStableWithinWindow()
        {
            ShopService shop = Build(out _, out _, out _, out FakeTimeProvider clock);

            IReadOnlyList<ShopSku> r1 = shop.GetCurrentCosmeticRotation();
            Assert.GreaterOrEqual(r1.Count, 3, "At least 3 SKUs (§7).");
            Assert.LessOrEqual(r1.Count, 5, "At most 5 SKUs (§7).");
            foreach (ShopSku sku in r1)
            {
                Assert.AreEqual(ShopSkuKind.Cosmetic, sku.Kind, "Rotation is cosmetic-only.");
            }

            // 13 days later: still the same window (14-day rotation).
            clock.Advance(TimeSpan.FromDays(13));
            CollectionAssert.AreEqual(r1, shop.GetCurrentCosmeticRotation(),
                "The rotation is stable within its 14-day window.");
        }

        [Test]
        public void Rotation_RollsForwardAfterFourteenDays()
        {
            ShopService shop = Build(out _, out _, out _, out FakeTimeProvider clock);
            int idx0 = shop.CurrentRotationIndex();

            clock.Advance(TimeSpan.FromDays(14));

            Assert.AreEqual(idx0 + 1, shop.CurrentRotationIndex(),
                "A new rotation begins after 14 days.");
        }

        [Test]
        public void ShardPacks_AreAlwaysAvailable_AsAPassiveRead()
        {
            ShopService shop = Build(out _, out _, out _, out _);

            IReadOnlyList<ShopSku> packs = shop.GetShardPacks();
            Assert.AreEqual(2, packs.Count, "Shard packs are always exposed in the Shop (§7).");
            foreach (ShopSku p in packs)
            {
                Assert.AreEqual(ShopSkuKind.ShardPack, p.Kind);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task BuyShardPack_CreditsShardsOnlyAfterServerValidation()
        {
            ShopService shop = Build(out Wallet wallet, out _, out FakeBackendClient backend, out _);
            ShopSku pack = shop.GetShardPacks()[0]; // grants 100 Shards

            // Server says invalid -> no Shards credited.
            backend.NextValidateReceipt = new ValidateReceiptResponse { Valid = false };
            ShopPurchaseResult bad = await shop.BuyShardPackAsync(pack, "jws.bad");
            Assert.IsFalse(bad.Success);
            Assert.AreEqual(0, wallet.GetBalance(CurrencyType.Shards), "No Shards on invalid receipt.");

            // Server says valid -> Shards credited.
            backend.NextValidateReceipt = new ValidateReceiptResponse
            {
                Valid = true,
                ProductId = pack.StoreKitProductId,
            };
            ShopPurchaseResult ok = await shop.BuyShardPackAsync(pack, "jws.good");
            Assert.IsTrue(ok.Success, ok.Message);
            Assert.AreEqual(2, backend.ValidateReceiptCalls, "Both purchases hit the validator.");
            Assert.AreEqual(100, wallet.GetBalance(CurrencyType.Shards));
        }

        [Test]
        public void BuyCosmetic_GrantsPermanentOwnership_AndIsIdempotent()
        {
            ShopService shop = Build(out Wallet wallet, out CosmeticState cosmetics, out _, out _,
                shards: 500);
            ShopSku sku = ShopSku.Cosmetic("cos.x", "skin.x", CurrencyType.Shards, 100);

            ShopPurchaseResult first = shop.BuyCosmetic(sku);
            Assert.IsTrue(first.Success, first.Message);
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "skin.x");
            Assert.AreEqual(400, wallet.GetBalance(CurrencyType.Shards));

            // Re-buying an owned cosmetic charges nothing.
            ShopPurchaseResult second = shop.BuyCosmetic(sku);
            Assert.IsTrue(second.Success);
            Assert.AreEqual(400, wallet.GetBalance(CurrencyType.Shards), "No double charge.");
            Assert.AreEqual(1, cosmetics.OwnedCosmeticIds.Count);
        }

        [Test]
        public void CosmeticPool_RejectsNonCosmeticEntries()
        {
            var badPool = new List<ShopSku>
            {
                ShopSku.ShardPackSku("p", "com.vyradata.keepfall.shards.starter", 100),
            };
            Assert.Throws<ArgumentException>(() => new ShopService(
                badPool, ShardPacks(), new Wallet(new WalletState()), new CosmeticState(),
                new RemoteConfig(), new FakeBackendClient()));
        }
    }
}
