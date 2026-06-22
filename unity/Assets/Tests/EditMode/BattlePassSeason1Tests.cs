using System.Collections.Generic;
using System.Linq;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Milestone 04 ("first 30-day season content ready", source-of-truth §7/§13). Validates the
    /// concrete Season 1 ("Sunset Watch") content: 12 cosmetics (5 free + 7 premium, within the
    /// Part B 8–12 SKU brief), all cosmetic-only and distinct, the free track completable F2P, and
    /// the premium track adding only cosmetics. The generic guarantees live in
    /// <c>BattlePassCosmeticOnlyTests</c>; this pins the shipped season.
    /// </summary>
    public sealed class BattlePassSeason1Tests
    {
        private static List<BattlePassReward> Season() => BattlePassSeason1.Rewards().ToList();

        [Test]
        public void Content_Is12DistinctCosmetics_5Free_7Premium_AllPrefixed()
        {
            List<BattlePassReward> s = Season();
            var ids = s.Select(r => r.CosmeticId).ToList();

            Assert.AreEqual(12, s.Count, "Season 1 defines 12 rewards.");
            Assert.AreEqual(12, ids.Distinct().Count(), "Every cosmetic id is distinct.");
            Assert.That(s.Count, Is.InRange(8, 12), "Within the Part B 8–12 SKU brief.");
            Assert.AreEqual(5, s.Count(r => r.Track == BattlePassTrack.Free), "5 free cosmetics.");
            Assert.AreEqual(7, s.Count(r => r.Track == BattlePassTrack.Premium), "7 premium cosmetics.");
            Assert.IsTrue(ids.All(i => i.StartsWith("cosmetic.s1.")),
                "Every Season 1 cosmetic id is namespaced cosmetic.s1.*");
            Assert.IsTrue(s.All(r => r.IsCosmetic), "Every reward is cosmetic (§7).");
        }

        [Test]
        public void SeasonLength_Is30TiersAnd30Days()
        {
            Assert.AreEqual(30, BattlePassSeason1.Tiers);
            Assert.AreEqual(30, BattlePassSeason1.SeasonDays);

            var pass = new BattlePassService(Season(), new CosmeticState(), new RemoteConfig());
            Assert.AreEqual(30, pass.MaxTier, "Top tier is 30.");
            Assert.AreEqual(30, pass.SeasonDays, "Season is 30 days (§7).");
        }

        [Test]
        public void FreeTrack_CompletableF2P_GrantsExactlyTheFiveFreeCosmetics()
        {
            var cosmetics = new CosmeticState();
            var pass = new BattlePassService(Season(), cosmetics, new RemoteConfig(), premiumOwned: false);

            pass.AddTierProgress(pass.MaxTier); // gameplay only, no spend
            Assert.IsTrue(pass.IsFreeTrackComplete(), "Free track must complete F2P (§7).");

            IReadOnlyList<string> granted = pass.ClaimUnlockedRewards();
            Assert.AreEqual(5, granted.Count, "Exactly the five free cosmetics.");
            Assert.AreEqual(5, cosmetics.OwnedCosmeticIds.Count, "No premium cosmetic granted unowned.");
            foreach (BattlePassReward r in Season().Where(r => r.Track == BattlePassTrack.Premium))
            {
                CollectionAssert.DoesNotContain(cosmetics.OwnedCosmeticIds, r.CosmeticId);
            }
        }

        [Test]
        public void PremiumOwned_GrantsAllTwelveCosmetics()
        {
            var cosmetics = new CosmeticState();
            var pass = new BattlePassService(Season(), cosmetics, new RemoteConfig(), premiumOwned: true);

            pass.AddTierProgress(pass.MaxTier);
            pass.ClaimUnlockedRewards();

            Assert.AreEqual(12, cosmetics.OwnedCosmeticIds.Count, "All 12 cosmetics owned with premium.");
            Assert.IsTrue(cosmetics.OwnedCosmeticIds.All(id => id.StartsWith("cosmetic.s1.")),
                "Only Season 1 cosmetics — no units, currency, or stats.");
        }
    }
}
