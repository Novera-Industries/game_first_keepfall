using System;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Locks in the Keepfall Plus HARD EXCLUSIONS (source-of-truth §6 + §10): no subscriber-only
    /// units, no subscriber-only tiles, no PvP perks, and NO combat advantage of any kind. Plus
    /// may only touch yield, deck slots, and economy convenience. If any of these drift to grant
    /// power, this test must fail.
    /// </summary>
    public sealed class PlusExclusionTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        private static PlusSubscription BuildActivePlus(
            out DeckState deck, out FakeTimeProvider clock)
        {
            clock = new FakeTimeProvider(Now);
            var sub = new SubscriptionState
            {
                Active = true,
                ProductId = PlusSubscription.PlusProductId,
                CurrentPeriodEndUtc = Now.AddDays(30),
            };
            var cosmetics = new CosmeticState();
            deck = new DeckState();
            var config = new RemoteConfig();
            var backend = new FakeBackendClient();
            return new PlusSubscription(sub, cosmetics, deck, config, backend,
                analytics: null, time: clock);
        }

        [Test]
        public void NoSubscriberOnlyUnitIsEverReachable()
        {
            // The guard is a compile-time/runtime constant false — for any unit id, any caller.
            foreach (string unitId in new[] { "vanguard.bulwark", "champion.captain", "master.001", "" })
            {
                Assert.IsFalse(PlusSubscription.IsUnitSubscriberLocked(unitId),
                    $"Plus must never lock unit '{unitId}' (§6, §10.2).");
            }
        }

        [Test]
        public void NoSubscriberOnlyTileOrRankIsEverReachable()
        {
            foreach (string tileId in new[] { "01", "07", "t3.premium", "" })
            {
                Assert.IsFalse(PlusSubscription.IsTileSubscriberLocked(tileId),
                    $"Plus must never lock tile '{tileId}' (§6).");
            }
        }

        [Test]
        public void PlusGrantsNoPvpPerk()
        {
            Assert.IsFalse(PlusSubscription.GrantsPvpPerk,
                "PvP is an inert Phase-2 placeholder; Plus grants no PvP perk (§0, §6).");
        }

        [Test]
        public void PlusModifiesNoCombatStat()
        {
            Assert.IsFalse(PlusSubscription.ModifiesCombatStats,
                "Plus confers no combat advantage of any kind (§6).");
        }

        [Test]
        public void ActivePlusPerks_AreConvenienceOnly_YieldSlotsEconomy()
        {
            PlusSubscription plus = BuildActivePlus(out DeckState deck, out FakeTimeProvider clock);

            // Yield: +50% (1.5x) — compresses earned time, never combat.
            Assert.AreEqual(1.5, plus.GetYieldMultiplier(), 1e-9, "Plus yield is +50%.");

            // Deck slots: +1 (3 -> 4). Loadout flexibility, not power.
            Assert.AreEqual(4, plus.GetEffectiveDeckSlots(), "Plus adds exactly one deck slot.");
            Assert.AreEqual(4, deck.SlotsUnlocked, "The +1 slot perk is applied to deck state.");

            // Economy convenience.
            Assert.AreEqual(2, plus.GetDailyQuestShardMultiplier(), "2x daily-quest Shards.");
            Assert.AreEqual(5, plus.GetDailyLoginShardBonus(), "+5 login Shards.");
            Assert.AreEqual(1, plus.GetFreeTierSkipsPerWeek(), "1 free BP tier skip/week.");
        }

        [Test]
        public void InactivePlus_ConfersNoPerks()
        {
            var clock = new FakeTimeProvider(Now);
            var sub = new SubscriptionState { Active = false };
            var deck = new DeckState();
            var plus = new PlusSubscription(sub, new CosmeticState(), deck, new RemoteConfig(),
                new FakeBackendClient(), analytics: null, time: clock);

            Assert.AreEqual(1.0, plus.GetYieldMultiplier(), 1e-9, "No yield bonus when inactive.");
            Assert.AreEqual(3, plus.GetEffectiveDeckSlots(), "F2P baseline slot count when inactive.");
            Assert.AreEqual(1, plus.GetDailyQuestShardMultiplier());
            Assert.AreEqual(0, plus.GetDailyLoginShardBonus());
            Assert.AreEqual(0, plus.GetFreeTierSkipsPerWeek());
        }

        [Test]
        public void CancellingPlus_DropsConvenienceSlotToBaseline_NeverBelow()
        {
            PlusSubscription plus = BuildActivePlus(out DeckState deck, out FakeTimeProvider clock);
            Assert.AreEqual(4, deck.SlotsUnlocked);

            plus.Cancel();

            Assert.AreEqual(3, deck.SlotsUnlocked,
                "Convenience slot lapses to the F2P baseline on cancel, never below.");
        }
    }
}
