using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Monetization;
using UnityEditor;
using UnityEngine;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// Editor on-ramp for the <c>milestone/02-shop-iap</c> deliverable: the StoreKit 2 Shard
    /// purchase loop, runnable without a device or App Store connection (source-of-truth §6, §7).
    /// These menu items print the canonical Shop state and simulate a sandbox purchase end to end
    /// (fabricated JWS → fake Worker validation → wallet credit) so the flow can be eyeballed in
    /// the Console.
    /// <para>Menu: <b>Keepfall ▸ Shop</b>.</para>
    /// </summary>
    public static class ShopDemoMenu
    {
        private const string LogPrefix = "[Keepfall][Shop] ";

        /// <summary>
        /// Logs the current 14-day cosmetic rotation and the always-available Shard packs (§7),
        /// using the canonical <see cref="IapCatalog"/>. Read-only — no purchase, no auto-present.
        /// </summary>
        [MenuItem("Keepfall/Shop/Log Current Rotation")]
        public static void LogCurrentRotation()
        {
            ShopService shop = BuildShop(out _, out _, out _);

            var sb = new StringBuilder();
            sb.AppendLine(LogPrefix + "Current Shop state (canonical catalog, source-of-truth §7).");

            IReadOnlyList<ShopSku> rotation = shop.GetCurrentCosmeticRotation();
            sb.AppendLine($"14-day cosmetic rotation (#{shop.CurrentRotationIndex()}) — {rotation.Count} SKUs:");
            foreach (ShopSku sku in rotation)
            {
                sb.AppendLine($"  cosmetic {sku.Id} -> {sku.CosmeticId} ({sku.Price} {sku.Currency})");
            }

            sb.AppendLine("Shard packs (always available in the Shop tab, never auto-presented after D3):");
            foreach (ShopSku pack in shop.GetShardPacks())
            {
                string usd = IapCatalog.UsdByProductId.TryGetValue(pack.StoreKitProductId, out decimal v)
                    ? "$" + v.ToString("0.00")
                    : "n/a";
                sb.AppendLine($"  {pack.Id}: {pack.ShardsGranted} Shards for {usd} ({pack.StoreKitProductId})");
            }

            Debug.Log(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Runs the starter-pack purchase loop against an editor-only fake backend: a
        /// <see cref="SandboxStoreKitPurchaser"/> fabricates a StoreKit-2-shaped JWS, the fake
        /// Worker validates it and returns the catalog Shard grant, and
        /// <see cref="ShopService.BuyShardPackAsync"/> credits the wallet. Logs the result and the
        /// new balance. DEV-only; no real purchase occurs.
        /// </summary>
        [MenuItem("Keepfall/Shop/Simulate Starter Pack Purchase")]
        public static void SimulateStarterPackPurchase()
        {
            _ = SimulateStarterPackPurchaseAsync();
        }

        private static async Task SimulateStarterPackPurchaseAsync()
        {
            ShopService shop = BuildShop(out Wallet wallet, out _, out _);
            ShopSku starter = IapCatalog.StarterPack;

            Debug.Log(LogPrefix + $"Simulating a sandbox purchase of {starter.Id} " +
                      $"({starter.StoreKitProductId}). Starting Shards: {wallet.GetBalance(CurrencyType.Shards)}.");

            var purchaser = new SandboxStoreKitPurchaser();
            StoreKitPurchase purchase = await purchaser.PurchaseAsync(starter.StoreKitProductId);
            if (!purchase.Success)
            {
                Debug.LogError(LogPrefix + $"Sandbox purchase failed: {purchase.Reason}.");
                return;
            }

            ShopPurchaseResult result = await shop.BuyShardPackAsync(starter, purchase.SignedTransaction);
            if (result.Success)
            {
                Debug.Log(LogPrefix + result.Message +
                          $" New Shards balance: {wallet.GetBalance(CurrencyType.Shards)}.");
            }
            else
            {
                Debug.LogError(LogPrefix + $"Purchase not credited ({result.Reason}): {result.Message}");
            }
        }

        /// <summary>
        /// Builds a fresh in-memory Shop wired to the canonical catalog and the editor fake
        /// backend. Each call starts from an empty wallet so the demo is repeatable.
        /// </summary>
        private static ShopService BuildShop(
            out Wallet wallet, out CosmeticState cosmetics, out EditorFakeBackendClient backend)
        {
            wallet = new Wallet(new WalletState());
            cosmetics = new CosmeticState();
            backend = new EditorFakeBackendClient();
            return new ShopService(
                IapCatalog.CosmeticPool,
                IapCatalog.ShardPacks,
                wallet,
                cosmetics,
                new RemoteConfig(),
                backend);
        }
    }
}
