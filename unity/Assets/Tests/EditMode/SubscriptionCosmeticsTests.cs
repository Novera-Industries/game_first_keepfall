using Keepfall.Core.State;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Guards the non-negotiable trust commitment (source-of-truth §6): cosmetics earned
    /// during a Keepfall Plus subscription are KEPT on cancellation.
    /// </summary>
    public sealed class SubscriptionCosmeticsTests
    {
        [Test]
        public void Cancellation_KeepsCosmeticsEarnedDuringSubscription()
        {
            var sub = new SubscriptionState
            {
                Active = true,
                ProductId = "keepfall.plus.monthly",
            };
            sub.CosmeticsGrantedDuringSub.Add("border.aurora");
            sub.CosmeticsGrantedDuringSub.Add("skin.bulwark.frost");

            var cosmetics = new CosmeticState();
            cosmetics.OwnedCosmeticIds.Add("skin.base"); // pre-existing F2P cosmetic

            SubscriptionCosmetics.KeepCosmeticsOnCancellation(sub, cosmetics);

            Assert.IsFalse(sub.Active, "Subscription perks lapse on cancellation.");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "skin.base");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "border.aurora");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "skin.bulwark.frost");
            Assert.AreEqual(3, cosmetics.OwnedCosmeticIds.Count);
        }

        [Test]
        public void Cancellation_IsDuplicateSafe_AndIdempotent()
        {
            var sub = new SubscriptionState { Active = true };
            sub.CosmeticsGrantedDuringSub.Add("border.aurora");

            var cosmetics = new CosmeticState();
            cosmetics.OwnedCosmeticIds.Add("border.aurora"); // already owned

            SubscriptionCosmetics.KeepCosmeticsOnCancellation(sub, cosmetics);
            SubscriptionCosmetics.KeepCosmeticsOnCancellation(sub, cosmetics);

            Assert.AreEqual(1, cosmetics.OwnedCosmeticIds.Count,
                "No duplicates, and re-running cancellation must not multiply ownership.");
        }
    }
}
