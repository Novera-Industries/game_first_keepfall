using System;
using System.Text;
using System.Threading.Tasks;
using Keepfall.Core.Backend;
using Keepfall.Monetization;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Verifies the DEV-only <see cref="SandboxStoreKitPurchaser"/> produces a StoreKit-2-shaped
    /// JWS the backend verifier would accept in non-production (source-of-truth §7): three
    /// segments, with the base64url-encoded middle (payload) segment decoding to JSON that carries
    /// the right <c>productId</c>, <c>bundleId</c>, and <c>type</c>. Pure — decoded with
    /// <see cref="System.Convert"/> / <see cref="Encoding"/>, no UnityEngine.
    /// </summary>
    public sealed class SandboxStoreKitPurchaserTests
    {
        private const string Starter = "com.vyradata.keepfall.shards.starter";

        [Test]
        public async Task PurchaseAsync_ReturnsSuccessWithAThreeSegmentJws()
        {
            var purchaser = new SandboxStoreKitPurchaser();
            StoreKitPurchase purchase = await purchaser.PurchaseAsync(Starter);

            Assert.IsTrue(purchase.Success, "A sandbox purchase succeeds.");
            Assert.IsNotNull(purchase.SignedTransaction);
            Assert.AreEqual(3, purchase.SignedTransaction.Split('.').Length,
                "The JWS has exactly three segments (header.payload.signature).");
        }

        [Test]
        public async Task PurchaseAsync_PayloadDecodesToConsumableJsonWithProductAndBundle()
        {
            var purchaser = new SandboxStoreKitPurchaser(); // defaults to a Consumable + bundle id
            StoreKitPurchase purchase = await purchaser.PurchaseAsync(Starter);

            string[] parts = purchase.SignedTransaction.Split('.');
            Assert.AreEqual(3, parts.Length);

            JObject payload = DecodePayload(parts[1]);

            Assert.AreEqual(Starter, (string)payload["productId"], "productId is stamped in the payload.");
            Assert.AreEqual(IapCatalog.BundleId, (string)payload["bundleId"],
                "bundleId matches the canonical bundle (the Worker cross-checks APP_BUNDLE_ID).");
            Assert.AreEqual("Consumable", (string)payload["type"],
                "A Shard pack is a Consumable (maps to consumable server-side).");
            Assert.IsFalse(string.IsNullOrEmpty((string)payload["transactionId"]),
                "A transactionId is present (drives idempotency).");
        }

        [Test]
        public async Task SubscriptionMode_StampsAutoRenewableType()
        {
            var purchaser = new SandboxStoreKitPurchaser(subscription: true);
            StoreKitPurchase purchase = await purchaser.PurchaseAsync(IapCatalog.PlusProductId);

            string[] parts = purchase.SignedTransaction.Split('.');
            JObject payload = DecodePayload(parts[1]);

            Assert.AreEqual("Auto-Renewable Subscription", (string)payload["type"],
                "Subscription mode stamps the subscription type (maps to subscription server-side).");
            Assert.AreEqual(IapCatalog.PlusProductId, (string)payload["productId"]);
        }

        [Test]
        public async Task TransactionIds_AreUniquePerCall()
        {
            var purchaser = new SandboxStoreKitPurchaser();
            JObject a = DecodePayload((await purchaser.PurchaseAsync(Starter)).SignedTransaction.Split('.')[1]);
            JObject b = DecodePayload((await purchaser.PurchaseAsync(Starter)).SignedTransaction.Split('.')[1]);

            Assert.AreNotEqual((string)a["transactionId"], (string)b["transactionId"],
                "Each sandbox purchase mints a unique transactionId.");
        }

        [Test]
        public void MissingProductId_FailsWithoutAJws()
        {
            var purchaser = new SandboxStoreKitPurchaser();
            StoreKitPurchase purchase = purchaser.PurchaseAsync(null).GetAwaiter().GetResult();

            Assert.IsFalse(purchase.Success);
            Assert.AreEqual("missing_product_id", purchase.Reason);
        }

        /// <summary>Decode a base64url segment to a JSON object (System.Convert + Encoding).</summary>
        private static JObject DecodePayload(string base64Url)
        {
            string b64 = base64Url.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(b64);
            string json = Encoding.UTF8.GetString(bytes);
            return JObject.Parse(json);
        }
    }
}
