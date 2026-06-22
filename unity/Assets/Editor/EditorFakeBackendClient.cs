using System;
using System.Threading;
using System.Threading.Tasks;
using Keepfall.Core.Backend;
using Keepfall.Monetization;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// EDITOR-ONLY. A tiny <see cref="IBackendClient"/> stand-in for the Shop demo menu. It
    /// approves a well-formed sandbox JWS (three segments) and returns the SERVER-AUTHORITATIVE
    /// Shard grant from the canonical catalog (<see cref="IapCatalog"/>), mirroring what the real
    /// Cloudflare Worker does on <c>POST /v1/receipts/validate</c> (source-of-truth §7).
    ///
    /// <para>This is NOT the real backend and never ships. It exists so
    /// <see cref="ShopDemoMenu"/> can run the purchase → validation → wallet-credit loop entirely
    /// in the editor. Only <see cref="ValidateReceiptAsync"/> is meaningful; the other
    /// <see cref="IBackendClient"/> methods throw <see cref="NotSupportedException"/>.</para>
    /// </summary>
    internal sealed class EditorFakeBackendClient : IBackendClient
    {
        public Task<ValidateReceiptResponse> ValidateReceiptAsync(
            ValidateReceiptRequest request, CancellationToken cancellationToken = default)
        {
            // Approve only a well-formed sandbox JWS (header.payload.signature).
            bool wellFormed =
                request != null &&
                !string.IsNullOrEmpty(request.SignedTransaction) &&
                request.SignedTransaction.Split('.').Length == 3;

            if (!wellFormed)
            {
                return Task.FromResult(new ValidateReceiptResponse { Valid = false });
            }

            // Keepfall Plus subscription (§6 Product 2): approve and report a 30-day period so the
            // Plus demo can activate perks. The real Worker derives this from the StoreKit 2 JWS.
            if (request.ProductId == PlusSubscription.PlusProductId)
            {
                return Task.FromResult(new ValidateReceiptResponse
                {
                    Valid = true,
                    ProductId = request.ProductId,
                    CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(30).ToString("o"),
                });
            }

            // Server-authoritative grant: look the product up in the canonical catalog (§7).
            int shards = ShardsForProduct(request.ProductId);
            if (shards <= 0)
            {
                // Unknown product — the real Worker rejects this; mirror by reporting invalid.
                return Task.FromResult(new ValidateReceiptResponse
                {
                    Valid = false,
                    ProductId = request.ProductId,
                });
            }

            return Task.FromResult(new ValidateReceiptResponse
            {
                Valid = true,
                ProductId = request.ProductId,
                ShardsGranted = shards,
                AlreadyProcessed = false,
            });
        }

        private static int ShardsForProduct(string productId)
        {
            foreach (var sku in IapCatalog.ShardPacks)
            {
                if (sku.StoreKitProductId == productId)
                {
                    return sku.ShardsGranted;
                }
            }

            return 0;
        }

        // ── Unused by the demo — fail loudly if ever called. ─────────────────
        public Task<CloudSavePushResponse> CloudSavePushAsync(
            CloudSavePushRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("EditorFakeBackendClient only supports ValidateReceiptAsync.");

        public Task<CloudSavePullResponse> CloudSavePullAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("EditorFakeBackendClient only supports ValidateReceiptAsync.");

        // Retry-token methods — canned positive verdicts so the difficulty/retry demo can show the
        // SERVER-AUTHORITATIVE redeem path (the real Worker enforces cannot-retry-a-win etc.).
        public Task<RequestRetryTokenResponse> RequestRetryTokenAsync(
            RequestRetryTokenRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RequestRetryTokenResponse { Eligible = true, Reason = null, TokenBalance = 1 });

        public Task<RedeemRetryTokenResponse> RedeemRetryTokenAsync(
            RedeemRetryTokenRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RedeemRetryTokenResponse
            {
                Redeemed = true,
                ReplaySeed = "sandbox-replay-seed",
                RewardsCappedToFirstAttempt = true,
                TokenBalance = 0,
            });

        public Task<GrantDailyRetryTokenResponse> GrantDailyRetryTokenAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GrantDailyRetryTokenResponse { Granted = true, TokenBalance = 1 });
    }
}
