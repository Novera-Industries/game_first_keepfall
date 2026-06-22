using System.Collections.Generic;
using System.Threading.Tasks;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// The server-authoritative Shard purchase flow (source-of-truth §7): the client credits the
    /// SERVER's Shard amount, only when the receipt is valid AND not a replay. An invalid verdict
    /// credits nothing; an <c>AlreadyProcessed</c> verdict credits nothing but reports success
    /// (idempotency — the player is never double-credited nor charged-without-credit).
    /// </summary>
    public sealed class ShardPurchaseFlowTests
    {
        private static ShopService Build(out Wallet wallet, out FakeBackendClient backend)
        {
            wallet = new Wallet(new WalletState());
            backend = new FakeBackendClient();
            return new ShopService(
                IapCatalog.CosmeticPool,
                IapCatalog.ShardPacks,
                wallet,
                new CosmeticState(),
                new RemoteConfig(),
                backend);
        }

        [Test]
        public async Task StarterPurchase_CreditsTheServerAmount()
        {
            ShopService shop = Build(out Wallet wallet, out FakeBackendClient backend);
            ShopSku starter = IapCatalog.StarterPack;

            // The Worker says valid and returns the authoritative grant of 100 Shards (§7).
            backend.NextValidateReceipt = new ValidateReceiptResponse
            {
                Valid = true,
                ProductId = starter.StoreKitProductId,
                ShardsGranted = 100,
                AlreadyProcessed = false,
            };

            ShopPurchaseResult result = await shop.BuyShardPackAsync(starter, "header.payload.sig");

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, backend.ValidateReceiptCalls, "The client validates server-side.");
            Assert.AreEqual(100, wallet.GetBalance(CurrencyType.Shards),
                "The client credits the SERVER amount (100).");
        }

        [Test]
        public async Task ServerAmount_OverridesTheClientCatalogValue()
        {
            ShopService shop = Build(out Wallet wallet, out FakeBackendClient backend);
            ShopSku starter = IapCatalog.StarterPack; // catalog says 100

            // Server is authoritative — even if it returned a different positive amount, the
            // client must credit the SERVER value, not its own catalog number.
            backend.NextValidateReceipt = new ValidateReceiptResponse
            {
                Valid = true,
                ProductId = starter.StoreKitProductId,
                ShardsGranted = 150,
                AlreadyProcessed = false,
            };

            await shop.BuyShardPackAsync(starter, "header.payload.sig");

            Assert.AreEqual(150, wallet.GetBalance(CurrencyType.Shards),
                "The server amount overrides the client catalog value.");
        }

        [Test]
        public async Task InvalidVerdict_CreditsNothing()
        {
            ShopService shop = Build(out Wallet wallet, out FakeBackendClient backend);
            ShopSku starter = IapCatalog.StarterPack;

            backend.NextValidateReceipt = new ValidateReceiptResponse { Valid = false };

            ShopPurchaseResult result = await shop.BuyShardPackAsync(starter, "header.payload.sig");

            Assert.IsFalse(result.Success, "An invalid receipt is not a successful purchase.");
            Assert.AreEqual(0, wallet.GetBalance(CurrencyType.Shards),
                "No Shards on an invalid verdict.");
        }

        [Test]
        public async Task AlreadyProcessed_CreditsNothing_ButReportsSuccess()
        {
            ShopService shop = Build(out Wallet wallet, out FakeBackendClient backend);
            ShopSku starter = IapCatalog.StarterPack;

            // Idempotency: the Worker still reports the grant, but the client must not re-credit.
            backend.NextValidateReceipt = new ValidateReceiptResponse
            {
                Valid = true,
                ProductId = starter.StoreKitProductId,
                ShardsGranted = 100,
                AlreadyProcessed = true,
            };

            ShopPurchaseResult result = await shop.BuyShardPackAsync(starter, "header.payload.sig");

            Assert.IsTrue(result.Success, "A replay is reported as a calm success.");
            Assert.AreEqual(0, wallet.GetBalance(CurrencyType.Shards),
                "An already-processed verdict credits nothing (no double-credit).");
        }
    }
}
