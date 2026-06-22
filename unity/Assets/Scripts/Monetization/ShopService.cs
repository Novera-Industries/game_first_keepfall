using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;

namespace Keepfall.Monetization
{
    /// <summary>
    /// What a Shop SKU sells. Both kinds are honest: a cosmetic (visual-only) or a Shard pack
    /// (premium currency the player spends on convenience). The Shop NEVER sells units, tiles,
    /// power, or a third currency (§1, §6, §10).
    /// </summary>
    public enum ShopSkuKind
    {
        /// <summary>A cosmetic (skin, border). Visual-only — no combat advantage (§7, §10).</summary>
        Cosmetic = 0,

        /// <summary>A Shard IAP pack (StoreKit 2). Always available in the Shop tab (§7).</summary>
        ShardPack = 1,
    }

    /// <summary>
    /// A single Shop entry. A cosmetic SKU is priced in <see cref="CurrencyType"/> (Stone for
    /// minor cosmetics, Shards for premium ones); a Shard pack is a real-money StoreKit product
    /// and carries a <see cref="StoreKitProductId"/> plus the Shards it credits on validation.
    /// </summary>
    public sealed class ShopSku
    {
        /// <summary>Stable SKU id (rotation key + analytics).</summary>
        public string Id { get; }

        /// <summary>Whether this is a cosmetic or a Shard pack.</summary>
        public ShopSkuKind Kind { get; }

        /// <summary>For a cosmetic: the cosmetic id granted into permanent ownership.</summary>
        public string CosmeticId { get; }

        /// <summary>For a cosmetic: the in-game currency it costs (Stone or Shards).</summary>
        public CurrencyType Currency { get; }

        /// <summary>For a cosmetic: the in-game price in <see cref="Currency"/>.</summary>
        public long Price { get; }

        /// <summary>For a Shard pack: the StoreKit 2 product id.</summary>
        public string StoreKitProductId { get; }

        /// <summary>For a Shard pack: how many Shards a validated purchase credits.</summary>
        public int ShardsGranted { get; }

        private ShopSku(
            string id, ShopSkuKind kind, string cosmeticId, CurrencyType currency, long price,
            string storeKitProductId, int shardsGranted)
        {
            Id = id;
            Kind = kind;
            CosmeticId = cosmeticId;
            Currency = currency;
            Price = price;
            StoreKitProductId = storeKitProductId;
            ShardsGranted = shardsGranted;
        }

        /// <summary>Creates a cosmetic SKU priced in an in-game currency.</summary>
        public static ShopSku Cosmetic(string id, string cosmeticId, CurrencyType currency, long price)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(cosmeticId))
            {
                throw new ArgumentException("A cosmetic SKU needs an id and a cosmetic id.");
            }

            if (price < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            return new ShopSku(id, ShopSkuKind.Cosmetic, cosmeticId, currency, price, null, 0);
        }

        /// <summary>Creates a Shard IAP pack SKU backed by a StoreKit 2 product.</summary>
        public static ShopSku ShardPackSku(string id, string storeKitProductId, int shardsGranted)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(storeKitProductId))
            {
                throw new ArgumentException("A Shard pack needs an id and a StoreKit product id.");
            }

            if (shardsGranted <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shardsGranted));
            }

            return new ShopSku(id, ShopSkuKind.ShardPack, null, CurrencyType.Shards, 0,
                storeKitProductId, shardsGranted);
        }
    }

    /// <summary>Outcome of a Shop purchase attempt.</summary>
    public readonly struct ShopPurchaseResult
    {
        /// <summary>True if the purchase completed.</summary>
        public bool Success { get; }

        /// <summary>Machine-readable reason on failure (e.g. "insufficient_funds").</summary>
        public string Reason { get; }

        /// <summary>Calm, §12-compliant UI message.</summary>
        public string Message { get; }

        internal ShopPurchaseResult(bool success, string reason, string message)
        {
            Success = success;
            Reason = reason;
            Message = message;
        }
    }

    /// <summary>
    /// Cosmetic Shop — source-of-truth §7. A 14-day rotation surfaces 3–5 cosmetic SKUs; Shard
    /// IAP packs are ALWAYS visible in the Shop tab and validated through
    /// <see cref="IBackendClient.ValidateReceiptAsync"/> (StoreKit 2). Everything here is
    /// cosmetic or premium-currency — never units, tiles, or power.
    ///
    /// <para>The Shop only EXPOSES Shard packs; it does not auto-present them. The decision of
    /// when (and whether) to surface a pack after D3 belongs to the funnel engine (§8) — this
    /// service has no app-open modal and no auto-present path.</para>
    ///
    /// Pure C# (no UnityEngine): the rotation index is derived from wall-clock days since a
    /// fixed epoch, so it is deterministic and testable with a <see cref="FakeTimeProvider"/>.
    /// </summary>
    public sealed class ShopService
    {
        private readonly IReadOnlyList<ShopSku> _cosmeticPool;
        private readonly IReadOnlyList<ShopSku> _shardPacks;
        private readonly Wallet _wallet;
        private readonly CosmeticState _cosmetics;
        private readonly RemoteConfig _config;
        private readonly IBackendClient _backend;
        private readonly IAnalytics _analytics;
        private readonly ITimeProvider _time;

        /// <summary>Rotation epoch — a fixed anchor so the 14-day window is deterministic.</summary>
        private static readonly DateTimeOffset RotationEpoch =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Constructs the Shop. <paramref name="cosmeticPool"/> is the full cosmetic catalog the
        /// rotation draws from (all cosmetic kind, validated here); <paramref name="shardPacks"/>
        /// are the always-available Shard IAP packs.
        /// </summary>
        public ShopService(
            IEnumerable<ShopSku> cosmeticPool,
            IEnumerable<ShopSku> shardPacks,
            Wallet wallet,
            CosmeticState cosmetics,
            RemoteConfig config,
            IBackendClient backend,
            IAnalytics analytics = null,
            ITimeProvider time = null)
        {
            if (cosmeticPool == null)
            {
                throw new ArgumentNullException(nameof(cosmeticPool));
            }

            if (shardPacks == null)
            {
                throw new ArgumentNullException(nameof(shardPacks));
            }

            var cosmetics2 = new List<ShopSku>(cosmeticPool);
            foreach (ShopSku sku in cosmetics2)
            {
                if (sku.Kind != ShopSkuKind.Cosmetic)
                {
                    throw new ArgumentException(
                        "The cosmetic pool may contain only cosmetic SKUs (§7).",
                        nameof(cosmeticPool));
                }
            }

            var packs = new List<ShopSku>(shardPacks);
            foreach (ShopSku sku in packs)
            {
                if (sku.Kind != ShopSkuKind.ShardPack)
                {
                    throw new ArgumentException(
                        "The Shard-pack list may contain only Shard-pack SKUs.",
                        nameof(shardPacks));
                }
            }

            _cosmeticPool = cosmetics2;
            _shardPacks = packs;
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _cosmetics = cosmetics ?? throw new ArgumentNullException(nameof(cosmetics));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _analytics = analytics;
            _time = time ?? GameClock.Provider;
        }

        /// <summary>Rotation length in days. Default 14 (§7), tunable via <c>shop.rotationDays</c>.</summary>
        public int RotationDays => Math.Max(1, _config.GetInt("shop.rotationDays", 14));

        /// <summary>Min SKUs surfaced per rotation. Default 3 (§7).</summary>
        public int MinSkusPerRotation => Math.Max(1, _config.GetInt("shop.skusPerRotation.min", 3));

        /// <summary>Max SKUs surfaced per rotation. Default 5 (§7).</summary>
        public int MaxSkusPerRotation => Math.Max(MinSkusPerRotation,
            _config.GetInt("shop.skusPerRotation.max", 5));

        /// <summary>Zero-based rotation index for "now" (whole 14-day windows since the epoch).</summary>
        public int CurrentRotationIndex()
        {
            double days = (_time.UtcNow - RotationEpoch).TotalDays;
            if (days < 0)
            {
                days = 0;
            }

            return (int)(days / RotationDays);
        }

        /// <summary>
        /// The 3–5 cosmetic SKUs for the current rotation, selected deterministically from the
        /// pool by the rotation index. Stable within a 14-day window; rolls forward on the next.
        /// </summary>
        public IReadOnlyList<ShopSku> GetCurrentCosmeticRotation()
        {
            var result = new List<ShopSku>();
            if (_cosmeticPool.Count == 0)
            {
                return result;
            }

            int rotation = CurrentRotationIndex();

            // Count is clamped to [min, max] and to the pool size, and varies deterministically
            // by rotation so successive windows feel fresh without randomness.
            int span = MaxSkusPerRotation - MinSkusPerRotation + 1;
            int count = MinSkusPerRotation + (rotation % span);
            count = Math.Min(count, _cosmeticPool.Count);

            int start = (rotation * count) % _cosmeticPool.Count;
            for (int i = 0; i < count; i++)
            {
                result.Add(_cosmeticPool[(start + i) % _cosmeticPool.Count]);
            }

            return result;
        }

        /// <summary>
        /// The Shard IAP packs. ALWAYS available in the Shop tab (§7). This is a passive read —
        /// it performs NO auto-present and NO app-open modal; the funnel engine alone decides
        /// when to surface a pack after D3 (§8).
        /// </summary>
        public IReadOnlyList<ShopSku> GetShardPacks() => _shardPacks;

        // ── Purchases ────────────────────────────────────────────────────

        /// <summary>
        /// Buys a cosmetic SKU with in-game currency (Stone or Shards). On success the cosmetic
        /// is granted into permanent <see cref="CosmeticState"/> ownership (kept forever, §6).
        /// Idempotent: re-buying an owned cosmetic spends nothing and reports success.
        /// </summary>
        public ShopPurchaseResult BuyCosmetic(ShopSku sku)
        {
            if (sku == null || sku.Kind != ShopSkuKind.Cosmetic)
            {
                return new ShopPurchaseResult(false, "not_a_cosmetic",
                    MonetizationStrings.RetryRefusedGeneric);
            }

            if (_cosmetics.OwnedCosmeticIds.Contains(sku.CosmeticId))
            {
                return new ShopPurchaseResult(true, "already_owned",
                    "You already own this. Nothing was charged.");
            }

            if (!_wallet.TrySpend(sku.Currency, sku.Price))
            {
                return new ShopPurchaseResult(false, "insufficient_funds",
                    "You do not have enough for this yet.");
            }

            _cosmetics.OwnedCosmeticIds.Add(sku.CosmeticId);

            _analytics?.Track(Events.ShardPackPurchase, new Dictionary<string, object>
            {
                ["sku"] = sku.Id,
                ["kind"] = "cosmetic",
                ["currency"] = sku.Currency.ToString(),
                ["price"] = sku.Price,
            });

            return new ShopPurchaseResult(true, null, "Cosmetic added to your collection.");
        }

        /// <summary>
        /// Buys a Shard IAP pack. The StoreKit 2 signed transaction is validated SERVER-SIDE via
        /// <see cref="IBackendClient.ValidateReceiptAsync"/>; only a valid verdict credits
        /// Shards. The client never grants premium currency on an unvalidated receipt.
        /// </summary>
        public async Task<ShopPurchaseResult> BuyShardPackAsync(
            ShopSku sku, string signedTransaction,
            CancellationToken cancellationToken = default)
        {
            if (sku == null || sku.Kind != ShopSkuKind.ShardPack)
            {
                return new ShopPurchaseResult(false, "not_a_shard_pack",
                    MonetizationStrings.RetryRefusedGeneric);
            }

            if (string.IsNullOrEmpty(signedTransaction))
            {
                return new ShopPurchaseResult(false, "missing_receipt",
                    "The purchase could not be completed. Please try again.");
            }

            ValidateReceiptResponse verdict = await _backend.ValidateReceiptAsync(
                new ValidateReceiptRequest
                {
                    SignedTransaction = signedTransaction,
                    ProductId = sku.StoreKitProductId,
                },
                cancellationToken);

            if (verdict == null || !verdict.Valid)
            {
                return new ShopPurchaseResult(false, "receipt_invalid",
                    "We could not verify that purchase. You were not charged for Shards.");
            }

            // Idempotency (§6/§7): a re-validated transaction reports the grant again, but the
            // client must NOT double-credit. Honour the server's verdict — credit nothing and
            // report a calm success so the player is never charged-without-credit nor double-paid.
            if (verdict.AlreadyProcessed)
            {
                return new ShopPurchaseResult(true, "already_processed",
                    "Those Shards were already added to your wallet.");
            }

            // The product→Shards mapping is SERVER-AUTHORITATIVE (§7): credit the amount the
            // Worker returned. Fall back to the SKU's catalog value only if the server omitted
            // it (older response), never preferring the client number over a positive server one.
            int credited = verdict.ShardsGranted > 0 ? verdict.ShardsGranted : sku.ShardsGranted;
            _wallet.Add(CurrencyType.Shards, credited);

            _analytics?.Track(Events.ShardPackPurchase, new Dictionary<string, object>
            {
                ["sku"] = sku.Id,
                ["kind"] = "shard_pack",
                ["product_id"] = sku.StoreKitProductId,
                ["shards_granted"] = credited,
            });

            return new ShopPurchaseResult(true, null, "Shards added to your wallet.");
        }
    }
}
