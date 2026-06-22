using System;
using System.Linq;
using System.Threading.Tasks;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Milestone 05 ("subscription renewal + cosmetic drop flow tested", source-of-truth §6/§13).
    /// Validates the Keepfall Plus monthly-drop CONTENT and the renewal→drop FLOW: across renewals
    /// each active month grants that month's cosmetic, and every drop is KEPT on cancellation (§6).
    /// The lifecycle/permanence guarantees are also covered by SubscriptionCosmeticPermanenceTests;
    /// this pins the shipped drop schedule and the multi-month flow.
    /// </summary>
    public sealed class PlusMonthlyDropFlowTests
    {
        private static readonly DateTimeOffset Start =
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        // ── Content ──────────────────────────────────────────────────────

        [Test]
        public void Schedule_Is12DistinctCosmetics_AllNamespaced()
        {
            var s = PlusMonthlyDrops.Schedule();
            Assert.AreEqual(12, s.Count);
            Assert.AreEqual(12, PlusMonthlyDrops.Months);
            Assert.AreEqual(12, s.Distinct().Count(), "Every monthly drop is distinct.");
            Assert.IsTrue(s.All(id => id.StartsWith("cosmetic.plus.")),
                "Every drop is a namespaced cosmetic — no unit/currency/stat.");
        }

        [Test]
        public void ForMonth_Is1Based_AndWrapsAfterTwelve()
        {
            Assert.AreEqual("cosmetic.plus.01.dawnward_vanguard", PlusMonthlyDrops.ForMonth(1));
            Assert.AreEqual("cosmetic.plus.12.golden_hour", PlusMonthlyDrops.ForMonth(12));
            Assert.AreEqual(PlusMonthlyDrops.ForMonth(1), PlusMonthlyDrops.ForMonth(13),
                "After month 12 the schedule repeats.");
            Assert.Throws<ArgumentOutOfRangeException>(() => PlusMonthlyDrops.ForMonth(0));
        }

        // ── Renewal → drop flow ──────────────────────────────────────────

        [Test]
        public async Task RenewalFlow_GrantsEachMonthsDrop_AndKeepsAllOnCancel()
        {
            var clock = new FakeTimeProvider(Start);
            var sub = new SubscriptionState();
            var cosmetics = new CosmeticState();
            var deck = new DeckState();
            var backend = new FakeBackendClient();
            var plus = new PlusSubscription(sub, cosmetics, deck, new RemoteConfig(), backend,
                analytics: null, time: clock);

            // Three active months across two renewals: each month grants its scheduled drop.
            for (int month = 1; month <= 3; month++)
            {
                backend.NextValidateReceipt = new ValidateReceiptResponse
                {
                    Valid = true,
                    ProductId = PlusSubscription.PlusProductId,
                    CurrentPeriodEndUtc = clock.UtcNow.AddDays(30).ToString("o"),
                };
                bool active = await plus.StartOrRenewAsync("jws.month" + month);
                Assert.IsTrue(active, $"Plus is active in month {month} after a valid (re)validation.");

                bool granted = plus.GrantMonthlyCosmetic(PlusMonthlyDrops.ForMonth(month));
                Assert.IsTrue(granted, $"Month {month} drop is granted while active.");

                clock.Advance(TimeSpan.FromDays(30)); // period lapses; the next loop renews
            }

            Assert.AreEqual(3, backend.ValidateReceiptCalls, "Each month re-validated server-side (renewal).");

            // Cancel after three months — every drop earned while subscribed is kept (§6).
            plus.Cancel();
            Assert.IsFalse(plus.IsActive);

            for (int month = 1; month <= 3; month++)
            {
                CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, PlusMonthlyDrops.ForMonth(month),
                    $"Month {month} drop must survive cancellation (§6).");
            }
            Assert.AreEqual(3, cosmetics.OwnedCosmeticIds.Count, "Exactly the three drops; nothing revoked.");
        }
    }
}
