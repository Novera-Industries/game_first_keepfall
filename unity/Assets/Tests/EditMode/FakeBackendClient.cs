using System;
using System.Threading;
using System.Threading.Tasks;
using Keepfall.Core.Backend;

namespace Keepfall.Tests
{
    /// <summary>
    /// Engine-free, scriptable <see cref="IBackendClient"/> for EditMode tests. It lets a test
    /// stand in for the Cloudflare Worker — the AUTHORITY for receipt validation and all
    /// retry-token rules (source-of-truth §6) — and assert that the client respects the server's
    /// verdict rather than deciding locally. Every method returns a pre-set response and records
    /// the last request it saw, so tests can verify the client called the server at all.
    /// </summary>
    public sealed class FakeBackendClient : IBackendClient
    {
        // ── Scripted responses (tests set these) ─────────────────────────
        public ValidateReceiptResponse NextValidateReceipt = new ValidateReceiptResponse
        {
            Valid = true,
            ProductId = "com.vyradata.keepfall.plus.monthly",
        };

        public RequestRetryTokenResponse NextRequestRetry = new RequestRetryTokenResponse
        {
            Eligible = true,
            TokenBalance = 0,
        };

        public RedeemRetryTokenResponse NextRedeem = new RedeemRetryTokenResponse
        {
            Redeemed = true,
            ReplaySeed = "seed-xyz",
            RewardsCappedToFirstAttempt = true,
            TokenBalance = 0,
        };

        public GrantDailyRetryTokenResponse NextGrantDaily = new GrantDailyRetryTokenResponse
        {
            Granted = true,
            TokenBalance = 1,
        };

        public CloudSavePushResponse NextPush = new CloudSavePushResponse { Accepted = true };
        public CloudSavePullResponse NextPull = new CloudSavePullResponse();

        // ── Call recording (tests assert the client reached the server) ──
        public int ValidateReceiptCalls { get; private set; }
        public int RedeemCalls { get; private set; }
        public int RequestRetryCalls { get; private set; }
        public int GrantDailyCalls { get; private set; }

        public ValidateReceiptRequest LastValidateReceiptRequest { get; private set; }
        public RedeemRetryTokenRequest LastRedeemRequest { get; private set; }
        public RequestRetryTokenRequest LastRequestRetryRequest { get; private set; }

        public Task<CloudSavePushResponse> CloudSavePushAsync(
            CloudSavePushRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(NextPush);

        public Task<CloudSavePullResponse> CloudSavePullAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NextPull);

        public Task<ValidateReceiptResponse> ValidateReceiptAsync(
            ValidateReceiptRequest request, CancellationToken cancellationToken = default)
        {
            ValidateReceiptCalls++;
            LastValidateReceiptRequest = request;
            return Task.FromResult(NextValidateReceipt);
        }

        public Task<RequestRetryTokenResponse> RequestRetryTokenAsync(
            RequestRetryTokenRequest request, CancellationToken cancellationToken = default)
        {
            RequestRetryCalls++;
            LastRequestRetryRequest = request;
            return Task.FromResult(NextRequestRetry);
        }

        public Task<RedeemRetryTokenResponse> RedeemRetryTokenAsync(
            RedeemRetryTokenRequest request, CancellationToken cancellationToken = default)
        {
            RedeemCalls++;
            LastRedeemRequest = request;
            return Task.FromResult(NextRedeem);
        }

        public Task<GrantDailyRetryTokenResponse> GrantDailyRetryTokenAsync(
            CancellationToken cancellationToken = default)
        {
            GrantDailyCalls++;
            return Task.FromResult(NextGrantDaily);
        }
    }
}
