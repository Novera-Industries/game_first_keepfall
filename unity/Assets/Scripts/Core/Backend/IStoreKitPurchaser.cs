using System.Threading;
using System.Threading.Tasks;

namespace Keepfall.Core.Backend
{
    /// <summary>
    /// Result of a StoreKit 2 purchase attempt. On success it carries the signed transaction
    /// (a JWS) the client forwards to the Worker for SERVER-SIDE validation
    /// (<see cref="IBackendClient.ValidateReceiptAsync"/>); the client never grants premium
    /// currency from this struct alone (source-of-truth §6, §7).
    /// </summary>
    public readonly struct StoreKitPurchase
    {
        /// <summary>True if StoreKit reported a completed purchase.</summary>
        public bool Success { get; }

        /// <summary>The StoreKit 2 signed transaction (JWS) to validate server-side.</summary>
        public string SignedTransaction { get; }

        /// <summary>Machine-readable reason on failure (e.g. "user_cancelled").</summary>
        public string Reason { get; }

        public StoreKitPurchase(bool success, string signedTransaction, string reason)
        {
            Success = success;
            SignedTransaction = signedTransaction;
            Reason = reason;
        }

        /// <summary>Convenience factory for a successful purchase.</summary>
        public static StoreKitPurchase Ok(string signedTransaction) =>
            new StoreKitPurchase(true, signedTransaction, null);

        /// <summary>Convenience factory for a failed purchase.</summary>
        public static StoreKitPurchase Fail(string reason) =>
            new StoreKitPurchase(false, null, reason);
    }

    /// <summary>
    /// Abstraction over the StoreKit 2 purchase call. The on-device implementation bridges to
    /// the native StoreKit 2 API; a sandbox implementation fabricates a backend-acceptable JWS
    /// for editor and CI testing. Keeping this an interface lets <see cref="ShopService"/> and
    /// the editor on-ramp drive the purchase flow without a live App Store connection.
    /// </summary>
    public interface IStoreKitPurchaser
    {
        /// <summary>
        /// Starts a purchase for <paramref name="productId"/> and returns the signed transaction
        /// on success. The caller then validates that transaction with the Worker before
        /// crediting anything (§6/§7).
        /// </summary>
        Task<StoreKitPurchase> PurchaseAsync(
            string productId, CancellationToken cancellationToken = default);
    }
}
