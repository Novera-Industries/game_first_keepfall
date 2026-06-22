using System.Collections.Generic;
using Keepfall.Core.Currency;

namespace Keepfall.Monetization
{
    /// <summary>
    /// The canonical client-side IAP + cosmetic catalog (source-of-truth §6, §7).
    ///
    /// <para><b>CANONICAL SOURCE:</b> the Shard pack ids, USD prices, and Shard amounts here
    /// MUST equal <c>config/iap-catalog.json</c> and the backend <c>SHARD_PACKS</c> map in
    /// <c>backend/src/lib/config.ts</c>. If you change a number, change all three. The
    /// product→Shards grant is ultimately SERVER-AUTHORITATIVE: the Worker validates the
    /// receipt and returns the Shard amount, and the client credits the server's value
    /// (<see cref="ShopService.BuyShardPackAsync"/>). The amounts below are the catalog mirror
    /// used for display and as a fallback only.</para>
    ///
    /// <para>Cosmetics are priced in in-game currency (Stone or Shards) and are NOT StoreKit
    /// products — that is why they are absent from <c>config/iap-catalog.json</c> (§7). The
    /// Keepfall Plus subscription is a StoreKit product but is owned by the subscription flow,
    /// so only its product id is exposed here.</para>
    /// </summary>
    public static class IapCatalog
    {
        /// <summary>App bundle id (matches <c>config/iap-catalog.json</c> and the backend).</summary>
        public const string BundleId = "com.vyradata.keepfall";

        // ── Shard pack product ids (§7 ladder) ───────────────────────────────
        public const string StarterProductId = "com.vyradata.keepfall.shards.starter";
        public const string PouchProductId = "com.vyradata.keepfall.shards.pouch";
        public const string ChestProductId = "com.vyradata.keepfall.shards.chest";
        public const string VaultProductId = "com.vyradata.keepfall.shards.vault";
        public const string HoardProductId = "com.vyradata.keepfall.shards.hoard";

        /// <summary>
        /// Keepfall Plus subscription product id — ONE tier, $5.99/month, 7-day trial
        /// (§6 Product 2). A subscription, not a Shard pack; surfaced here for reference only.
        /// </summary>
        public const string PlusProductId = "com.vyradata.keepfall.plus.monthly";

        /// <summary>
        /// USD reference prices for the five Shard packs (Apple default tiers). Mirrors
        /// <c>config/iap-catalog.json</c>. Real storefronts are CA/AU/NZ; these USD values are
        /// the canonical reference and are used by tests to assert the value ladder (§7).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, decimal> UsdByProductId =
            new Dictionary<string, decimal>
            {
                [StarterProductId] = 0.99m,
                [PouchProductId] = 4.99m,
                [ChestProductId] = 9.99m,
                [VaultProductId] = 19.99m,
                [HoardProductId] = 49.99m,
            };

        /// <summary>
        /// The five repeatable Shard IAP packs (§7), in ascending price/size order. Effective
        /// $/Shard improves with size — honest bulk convenience, never an outcome (§6, §10).
        /// <c>starter</c> doubles as the D3 single-banner offer (§8). Always built fresh so a
        /// caller can never mutate the shared list.
        /// </summary>
        public static IReadOnlyList<ShopSku> ShardPacks => new List<ShopSku>
        {
            // id            productId           shards   (usd, for reference: see UsdByProductId)
            ShopSku.ShardPackSku("starter", StarterProductId, 100),   // 0.99 — also the D3 starter
            ShopSku.ShardPackSku("pouch", PouchProductId, 550),       // 4.99
            ShopSku.ShardPackSku("chest", ChestProductId, 1200),      // 9.99
            ShopSku.ShardPackSku("vault", VaultProductId, 2600),      // 19.99
            ShopSku.ShardPackSku("hoard", HoardProductId, 7000),      // 49.99
        };

        /// <summary>
        /// The <c>starter</c> Shard pack — the D3 first offer shown as a single banner (§8).
        /// </summary>
        public static ShopSku StarterPack =>
            ShopSku.ShardPackSku("starter", StarterProductId, 100);

        /// <summary>
        /// A small starter cosmetic pool the 14-day Shop rotation draws from (§7). Cosmetics are
        /// visual-only — tile banners, profile borders, and a victory emote frame (see
        /// <c>docs/design-system.md</c> §2.6) — priced in Stone (minor) or Shards (premium).
        /// They confer NO combat advantage and are NOT StoreKit products (§7, §10). Built fresh
        /// each call so the shared list is never mutated.
        /// </summary>
        public static IReadOnlyList<ShopSku> CosmeticPool => new List<ShopSku>
        {
            // Tile banners — minor cosmetics, Stone-priced.
            ShopSku.Cosmetic("cos.banner.dusk", "banner.dusk", CurrencyType.Stone, 600),
            ShopSku.Cosmetic("cos.banner.emberHorizon", "banner.ember_horizon", CurrencyType.Stone, 600),
            // Profile borders — premium cosmetics, Shard-priced.
            ShopSku.Cosmetic("cos.border.indigoLeaf", "border.indigo_leaf", CurrencyType.Shards, 120),
            ShopSku.Cosmetic("cos.border.magentaTide", "border.magenta_tide", CurrencyType.Shards, 120),
            // Victory emote frame — premium cosmetic, Shard-priced.
            ShopSku.Cosmetic("cos.frame.calmVictory", "frame.calm_victory", CurrencyType.Shards, 180),
        };
    }
}
