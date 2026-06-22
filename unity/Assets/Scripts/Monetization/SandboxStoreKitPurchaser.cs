using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Keepfall.Core.Backend;

namespace Keepfall.Monetization
{
    /// <summary>
    /// DEV / SANDBOX ONLY. A <see cref="IStoreKitPurchaser"/> that FABRICATES a StoreKit-2-shaped
    /// signed transaction (a three-segment JWS <c>header.payload.signature</c>) whose payload the
    /// Worker accepts when <c>ENVIRONMENT != "production"</c>
    /// (<c>backend/src/lib/receipts.ts → appleStoreKitVerifier</c>). It performs NO real purchase
    /// and contacts NO App Store.
    ///
    /// <para><b>Never used on device or in production.</b> The header and signature segments are
    /// dummy base64url; only the payload is meaningful, and the production verifier hard-stops on
    /// these (it requires the Apple Root CA - G3 chain). This exists so the purchase → Worker
    /// validation → wallet-credit loop can be exercised in the editor and in CI without Apple.</para>
    ///
    /// <para>The base64url encoder is pure (no UnityEngine) so the produced JWS can be unit-tested
    /// and decoded with <see cref="System.Convert"/> / <see cref="System.Text.Encoding"/>.</para>
    /// </summary>
    public sealed class SandboxStoreKitPurchaser : IStoreKitPurchaser
    {
        private readonly string _bundleId;
        private readonly bool _subscription;
        private readonly Func<DateTimeOffset> _now;
        private int _counter;

        /// <summary>The StoreKit 2 transaction type string for a Shard consumable pack.</summary>
        public const string ConsumableType = "Consumable";

        /// <summary>The StoreKit 2 transaction type string for the Plus subscription.</summary>
        public const string SubscriptionType = "Auto-Renewable Subscription";

        /// <summary>
        /// Creates a sandbox purchaser.
        /// </summary>
        /// <param name="bundleId">Bundle id stamped into the payload; must match the Worker's
        /// <c>APP_BUNDLE_ID</c>. Defaults to <see cref="IapCatalog.BundleId"/>.</param>
        /// <param name="subscription">When true, the payload's <c>type</c> is
        /// "Auto-Renewable Subscription" (for the Plus product) instead of "Consumable".</param>
        /// <param name="now">Clock for <c>purchaseDate</c>; defaults to UTC wall clock.</param>
        public SandboxStoreKitPurchaser(
            string bundleId = IapCatalog.BundleId,
            bool subscription = false,
            Func<DateTimeOffset> now = null)
        {
            _bundleId = string.IsNullOrEmpty(bundleId) ? IapCatalog.BundleId : bundleId;
            _subscription = subscription;
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        /// <inheritdoc />
        public Task<StoreKitPurchase> PurchaseAsync(
            string productId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return Task.FromResult(StoreKitPurchase.Fail("missing_product_id"));
            }

            string jws = FabricateJws(productId);
            return Task.FromResult(StoreKitPurchase.Ok(jws));
        }

        /// <summary>
        /// Builds the three-segment JWS the non-production Worker accepts. The payload carries
        /// <c>bundleId</c>, <c>productId</c>, a unique <c>transactionId</c> (also used as
        /// <c>originalTransactionId</c>), <c>type</c>, and <c>purchaseDate</c> (ms). Header and
        /// signature are dummy base64url. Pure and deterministic given the clock + counter.
        /// </summary>
        public string FabricateJws(string productId)
        {
            string transactionId = NewTransactionId();
            long purchaseDateMs = _now().ToUnixTimeMilliseconds();
            string type = _subscription ? SubscriptionType : ConsumableType;

            // Minimal payload — order/spacing is not significant; this is parsed as JSON.
            string payloadJson =
                "{" +
                "\"bundleId\":\"" + Escape(_bundleId) + "\"," +
                "\"productId\":\"" + Escape(productId) + "\"," +
                "\"transactionId\":\"" + Escape(transactionId) + "\"," +
                "\"originalTransactionId\":\"" + Escape(transactionId) + "\"," +
                "\"type\":\"" + type + "\"," +
                "\"purchaseDate\":" + purchaseDateMs +
                "}";

            // Dummy, well-formed header (alg/typ) and signature. The non-production verifier
            // ignores both; only the payload is read. Production rejects this whole token.
            string header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"JWT\"}"));
            string payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            string signature = Base64UrlEncode(Encoding.UTF8.GetBytes("sandbox-not-a-real-signature"));

            return header + "." + payload + "." + signature;
        }

        private string NewTransactionId()
        {
            int n = Interlocked.Increment(ref _counter);
            // Unique per call (counter + guid) so replays/idempotency can be exercised.
            return "sandbox-" + n + "-" + Guid.NewGuid().ToString("N");
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>
        /// Pure base64url encoder (RFC 4648 §5, no padding): standard base64 with <c>+ → -</c>,
        /// <c>/ → _</c>, and trailing <c>=</c> stripped. Matches the Worker's base64url decoder.
        /// No UnityEngine dependency, so this is unit-testable.
        /// </summary>
        public static string Base64UrlEncode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
