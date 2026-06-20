using System.Collections.Generic;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Canonical, non-negotiable logic for the trust commitment in source-of-truth §6:
    /// <b>cosmetics earned during a Keepfall Plus subscription are KEPT on cancellation.</b>
    /// The Monetization/subscription feature MUST route cancellation through
    /// <see cref="KeepCosmeticsOnCancellation"/> rather than clearing anything. Pure logic so
    /// the guarantee is unit-tested in EditMode.
    /// </summary>
    public static class SubscriptionCosmetics
    {
        /// <summary>
        /// Folds every cosmetic granted during the subscription into permanent
        /// <see cref="CosmeticState"/> ownership and marks the subscription inactive. Nothing
        /// is ever removed from <see cref="CosmeticState.OwnedCosmeticIds"/>. Idempotent and
        /// duplicate-safe.
        /// </summary>
        public static void KeepCosmeticsOnCancellation(
            SubscriptionState subscription, CosmeticState cosmetics)
        {
            if (subscription == null || cosmetics == null)
            {
                return;
            }

            var owned = new HashSet<string>(cosmetics.OwnedCosmeticIds);
            foreach (string id in subscription.CosmeticsGrantedDuringSub)
            {
                if (!string.IsNullOrEmpty(id) && owned.Add(id))
                {
                    cosmetics.OwnedCosmeticIds.Add(id);
                }
            }

            // Subscription perks lapse, but ownership above is permanent.
            subscription.Active = false;
        }
    }
}
