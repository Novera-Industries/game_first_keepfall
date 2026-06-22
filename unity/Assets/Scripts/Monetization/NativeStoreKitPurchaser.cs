using System.Threading;
using System.Threading.Tasks;
using Keepfall.Core.Backend;

namespace Keepfall.Monetization
{
    /// <summary>
    /// On-device StoreKit 2 purchaser — STUB. This is where the real iOS purchase lives: a
    /// native Objective-C / Swift bridge that calls StoreKit 2's
    /// <c>Product.purchase()</c>, awaits the <c>Transaction</c>, and returns its
    /// <c>jwsRepresentation</c> (the signed transaction) for the Worker to validate server-side
    /// (source-of-truth §6, §7).
    ///
    /// <para><b>Intended native flow (to implement in milestone 02/08 on a Mac with Xcode):</b>
    /// <list type="number">
    ///   <item>Load <c>Product</c>s for the Shard pack ids (<see cref="IapCatalog.ShardPacks"/>).</item>
    ///   <item>Call <c>product.purchase()</c>; handle <c>.success(.verified(transaction))</c>,
    ///         <c>.userCancelled</c>, and <c>.pending</c>.</item>
    ///   <item>Return <c>transaction.jwsRepresentation</c> as
    ///         <see cref="StoreKitPurchase.SignedTransaction"/>.</item>
    ///   <item>Call <c>transaction.finish()</c> only AFTER the Worker confirms the credit.</item>
    /// </list></para>
    ///
    /// <para><b>No fake transactions on device.</b> Off-device or in the editor there is no
    /// StoreKit, so this returns <c>Success:false, reason:"native_storekit_unavailable"</c>
    /// rather than fabricating a receipt. For editor/CI testing use
    /// <see cref="SandboxStoreKitPurchaser"/> instead.</para>
    /// </summary>
    public sealed class NativeStoreKitPurchaser : IStoreKitPurchaser
    {
        /// <inheritdoc />
        public Task<StoreKitPurchase> PurchaseAsync(
            string productId, CancellationToken cancellationToken = default)
        {
#if UNITY_IOS && !UNITY_EDITOR
            // TODO(milestone-02/08, on-device): invoke the native StoreKit 2 bridge described in
            // the class summary and return transaction.jwsRepresentation. Until the bridge is
            // wired, fail honestly rather than fabricate a transaction.
            return Task.FromResult(StoreKitPurchase.Fail("native_storekit_not_implemented"));
#else
            // Off-device / editor: there is no StoreKit. Never fabricate a receipt here.
            return Task.FromResult(StoreKitPurchase.Fail("native_storekit_unavailable"));
#endif
        }
    }
}
