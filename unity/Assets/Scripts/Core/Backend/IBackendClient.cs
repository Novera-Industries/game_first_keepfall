using System.Threading;
using System.Threading.Tasks;

namespace Keepfall.Core.Backend
{
    /// <summary>
    /// Client contract for the Cloudflare Worker API (source-of-truth §11). The Worker is the
    /// AUTHORITY for receipt validation and all retry-token rules (§6 Product 3): cannot retry
    /// a win, cannot retry a retry, rewards capped at the first-attempt rate. The client only
    /// requests and renders; it never decides these outcomes locally. All methods are async
    /// and accept a <see cref="CancellationToken"/> for scene-change/teardown cancellation.
    /// </summary>
    public interface IBackendClient
    {
        /// <summary>Pushes the local save to cloud. <c>POST /v1/save</c>.</summary>
        Task<CloudSavePushResponse> CloudSavePushAsync(
            CloudSavePushRequest request, CancellationToken cancellationToken = default);

        /// <summary>Pulls the latest cloud save. <c>GET /v1/save</c>.</summary>
        Task<CloudSavePullResponse> CloudSavePullAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Validates a StoreKit 2 receipt server-side. <c>POST /v1/receipts/validate</c>.</summary>
        Task<ValidateReceiptResponse> ValidateReceiptAsync(
            ValidateReceiptRequest request, CancellationToken cancellationToken = default);

        /// <summary>Asks whether a match may be retried. <c>POST /v1/retry/request</c>.</summary>
        Task<RequestRetryTokenResponse> RequestRetryTokenAsync(
            RequestRetryTokenRequest request, CancellationToken cancellationToken = default);

        /// <summary>Redeems a retry token for an exact replay seed. <c>POST /v1/retry/redeem</c>.</summary>
        Task<RedeemRetryTokenResponse> RedeemRetryTokenAsync(
            RedeemRetryTokenRequest request, CancellationToken cancellationToken = default);

        /// <summary>Requests the daily login retry-token grant. <c>POST /v1/retry/grant-daily</c>.</summary>
        Task<GrantDailyRetryTokenResponse> GrantDailyRetryTokenAsync(
            CancellationToken cancellationToken = default);
    }
}
