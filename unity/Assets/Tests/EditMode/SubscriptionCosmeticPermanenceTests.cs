using System;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// THE REQUIRED permanence test (source-of-truth §6 trust commitment): cosmetics granted
    /// while Keepfall Plus is active are KEPT on cancellation AND on expiry, and NONE are ever
    /// revoked. Exercises the full <see cref="PlusSubscription"/> lifecycle, not just the Core
    /// migration helper.
    /// </summary>
    public sealed class SubscriptionCosmeticPermanenceTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        private static PlusSubscription BuildPlus(
            out SubscriptionState sub, out CosmeticState cosmetics, out FakeTimeProvider clock,
            out FakeBackendClient backend)
        {
            clock = new FakeTimeProvider(Now);
            sub = new SubscriptionState();
            cosmetics = new CosmeticState();
            var deck = new DeckState();
            var config = new RemoteConfig();
            backend = new FakeBackendClient();
            return new PlusSubscription(sub, cosmetics, deck, config, backend,
                analytics: null, time: clock);
        }

        [Test]
        public async System.Threading.Tasks.Task Cancellation_KeepsEveryCosmeticGrantedWhileSubscribed()
        {
            PlusSubscription plus = BuildPlus(out SubscriptionState sub, out CosmeticState cosmetics,
                out FakeTimeProvider clock, out FakeBackendClient backend);

            // A pre-existing F2P cosmetic the player owned before subscribing.
            cosmetics.OwnedCosmeticIds.Add("skin.base");

            // Server validates the subscription; perks go active.
            backend.NextValidateReceipt = new Keepfall.Core.Backend.ValidateReceiptResponse
            {
                Valid = true,
                ProductId = PlusSubscription.PlusProductId,
                CurrentPeriodEndUtc = Now.AddDays(30).ToString("o"),
            };
            bool active = await plus.StartOrRenewAsync("jws.txn", asTrial: false);
            Assert.IsTrue(active, "A valid receipt activates Plus.");

            // Three monthly cosmetic drops earned during the subscription.
            Assert.IsTrue(plus.GrantMonthlyCosmetic("border.aurora"));
            Assert.IsTrue(plus.GrantMonthlyCosmetic("skin.bulwark.frost"));
            Assert.IsTrue(plus.GrantMonthlyCosmetic("border.ember"));

            // Cancel.
            plus.Cancel();

            Assert.IsFalse(plus.IsActive, "Perks lapse after cancellation.");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "skin.base");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "border.aurora");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "skin.bulwark.frost");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "border.ember");
            Assert.AreEqual(4, cosmetics.OwnedCosmeticIds.Count,
                "All sub cosmetics + the pre-existing one are owned; nothing was revoked.");
        }

        [Test]
        public async System.Threading.Tasks.Task Expiry_AlsoKeepsCosmetics_WhenCancellationRunsAfterPeriodEnd()
        {
            PlusSubscription plus = BuildPlus(out SubscriptionState sub, out CosmeticState cosmetics,
                out FakeTimeProvider clock, out FakeBackendClient backend);

            backend.NextValidateReceipt = new Keepfall.Core.Backend.ValidateReceiptResponse
            {
                Valid = true,
                ProductId = PlusSubscription.PlusProductId,
                CurrentPeriodEndUtc = Now.AddDays(30).ToString("o"),
            };
            await plus.StartOrRenewAsync("jws.txn");
            plus.GrantMonthlyCosmetic("border.aurora");

            // Time passes beyond the paid period — Plus is no longer active by the period check.
            clock.Advance(TimeSpan.FromDays(45));
            Assert.IsFalse(plus.IsActive, "Lapsed period reads as inactive.");

            // StoreKit fires the lapse; the cancel path still keeps the cosmetic.
            plus.Cancel();

            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "border.aurora");
            Assert.AreEqual(1, cosmetics.OwnedCosmeticIds.Count);
        }

        [Test]
        public async System.Threading.Tasks.Task Cancellation_IsIdempotent_AndNeverRevokes()
        {
            PlusSubscription plus = BuildPlus(out SubscriptionState sub, out CosmeticState cosmetics,
                out FakeTimeProvider clock, out FakeBackendClient backend);

            backend.NextValidateReceipt = new Keepfall.Core.Backend.ValidateReceiptResponse
            {
                Valid = true,
                ProductId = PlusSubscription.PlusProductId,
                CurrentPeriodEndUtc = Now.AddDays(30).ToString("o"),
            };
            await plus.StartOrRenewAsync("jws.txn");
            plus.GrantMonthlyCosmetic("border.aurora");

            plus.Cancel();
            plus.Cancel(); // running cancellation twice must not duplicate or remove anything

            Assert.AreEqual(1, cosmetics.OwnedCosmeticIds.Count,
                "Idempotent: no duplicates, no revocation.");
            CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, "border.aurora");
        }

        [Test]
        public async System.Threading.Tasks.Task GrantMonthlyCosmetic_NoOpWhenInactive()
        {
            PlusSubscription plus = BuildPlus(out SubscriptionState sub, out CosmeticState cosmetics,
                out FakeTimeProvider clock, out FakeBackendClient backend);

            // Never subscribed: a monthly drop cannot be granted.
            Assert.IsFalse(plus.GrantMonthlyCosmetic("border.aurora"));
            Assert.AreEqual(0, cosmetics.OwnedCosmeticIds.Count);
            await System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
