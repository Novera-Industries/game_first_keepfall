using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Keepfall.Core.Backend
{
    /// <summary>
    /// Minimal HTTP implementation of <see cref="IBackendClient"/> over UnityWebRequest. The
    /// CONTRACT (routes, verbs, DTOs) is the point of this class; the transport is deliberately
    /// thin and will be hardened (auth headers, retries, telemetry) by the backend-integration
    /// task. Routes match the Cloudflare Worker agent's API:
    /// <list type="bullet">
    ///   <item><c>GET  /v1/save</c> — pull cloud save</item>
    ///   <item><c>POST /v1/save</c> — push cloud save</item>
    ///   <item><c>POST /v1/receipts/validate</c> — StoreKit 2 receipt validation</item>
    ///   <item><c>POST /v1/retry/request</c> — retry eligibility check</item>
    ///   <item><c>POST /v1/retry/redeem</c> — redeem retry token (returns replay seed)</item>
    ///   <item><c>POST /v1/retry/grant-daily</c> — daily login token grant</item>
    /// </list>
    /// The Worker is authoritative: this client never enforces the retry rules itself.
    /// </summary>
    public sealed class HttpBackendClient : IBackendClient
    {
        private const string RouteSave = "/v1/save";
        private const string RouteValidateReceipt = "/v1/receipts/validate";
        private const string RouteRetryRequest = "/v1/retry/request";
        private const string RouteRetryRedeem = "/v1/retry/redeem";
        private const string RouteRetryGrantDaily = "/v1/retry/grant-daily";

        private readonly string _baseUrl;
        private readonly Func<string> _authTokenProvider;

        /// <summary>
        /// Creates a client targeting <paramref name="baseUrl"/> (no trailing slash, e.g.
        /// "https://api.keepfall.app"). <paramref name="authTokenProvider"/> supplies the
        /// bearer token for each request; pass null during early bring-up.
        /// </summary>
        public HttpBackendClient(string baseUrl, Func<string> authTokenProvider = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            }

            _baseUrl = baseUrl.TrimEnd('/');
            _authTokenProvider = authTokenProvider;
        }

        /// <inheritdoc />
        public Task<CloudSavePushResponse> CloudSavePushAsync(
            CloudSavePushRequest request, CancellationToken cancellationToken = default) =>
            SendAsync<CloudSavePushRequest, CloudSavePushResponse>(
                UnityWebRequest.kHttpVerbPOST, RouteSave, request, cancellationToken);

        /// <inheritdoc />
        public Task<CloudSavePullResponse> CloudSavePullAsync(
            CancellationToken cancellationToken = default) =>
            SendAsync<object, CloudSavePullResponse>(
                UnityWebRequest.kHttpVerbGET, RouteSave, null, cancellationToken);

        /// <inheritdoc />
        public Task<ValidateReceiptResponse> ValidateReceiptAsync(
            ValidateReceiptRequest request, CancellationToken cancellationToken = default) =>
            SendAsync<ValidateReceiptRequest, ValidateReceiptResponse>(
                UnityWebRequest.kHttpVerbPOST, RouteValidateReceipt, request, cancellationToken);

        /// <inheritdoc />
        public Task<RequestRetryTokenResponse> RequestRetryTokenAsync(
            RequestRetryTokenRequest request, CancellationToken cancellationToken = default) =>
            SendAsync<RequestRetryTokenRequest, RequestRetryTokenResponse>(
                UnityWebRequest.kHttpVerbPOST, RouteRetryRequest, request, cancellationToken);

        /// <inheritdoc />
        public Task<RedeemRetryTokenResponse> RedeemRetryTokenAsync(
            RedeemRetryTokenRequest request, CancellationToken cancellationToken = default) =>
            SendAsync<RedeemRetryTokenRequest, RedeemRetryTokenResponse>(
                UnityWebRequest.kHttpVerbPOST, RouteRetryRedeem, request, cancellationToken);

        /// <inheritdoc />
        public Task<GrantDailyRetryTokenResponse> GrantDailyRetryTokenAsync(
            CancellationToken cancellationToken = default) =>
            SendAsync<object, GrantDailyRetryTokenResponse>(
                UnityWebRequest.kHttpVerbPOST, RouteRetryGrantDaily, null, cancellationToken);

        private async Task<TResponse> SendAsync<TRequest, TResponse>(
            string verb, string route, TRequest body, CancellationToken cancellationToken)
        {
            string url = _baseUrl + route;
            using var request = new UnityWebRequest(url, verb)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };

            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            string token = _authTokenProvider?.Invoke();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new BackendException(
                    $"{verb} {route} failed: {request.responseCode} {request.error}",
                    (int)request.responseCode);
            }

            string responseText = request.downloadHandler.text;
            return string.IsNullOrEmpty(responseText)
                ? default
                : JsonConvert.DeserializeObject<TResponse>(responseText);
        }
    }

    /// <summary>Thrown when a backend call fails at the transport/HTTP layer.</summary>
    public sealed class BackendException : Exception
    {
        /// <summary>HTTP status code (0 if no response was received).</summary>
        public int StatusCode { get; }

        /// <summary>Creates the exception with a message and status code.</summary>
        public BackendException(string message, int statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
