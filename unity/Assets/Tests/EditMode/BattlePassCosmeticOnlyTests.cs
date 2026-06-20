using System;
using System.Collections.Generic;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// THE REQUIRED Battle Pass test (source-of-truth §7): every reward on BOTH tracks is a
    /// cosmetic, the free track is completable F2P, and the tier-skip consumable advances tier
    /// progress ONLY — it carries no power and grants no units, Stone, or Shards.
    /// </summary>
    public sealed class BattlePassCosmeticOnlyTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        private static List<BattlePassReward> SampleSeason()
        {
            // A small season: 5 free-track + 5 premium-track cosmetics across tiers 1..5.
            var rewards = new List<BattlePassReward>();
            for (int tier = 1; tier <= 5; tier++)
            {
                rewards.Add(new BattlePassReward(tier, BattlePassTrack.Free, $"free.skin.{tier}"));
                rewards.Add(new BattlePassReward(tier, BattlePassTrack.Premium, $"prem.border.{tier}"));
            }

            return rewards;
        }

        private static BattlePassService BuildPass(
            out CosmeticState cosmetics, bool premiumOwned = false)
        {
            cosmetics = new CosmeticState();
            var clock = new FakeTimeProvider(Now);
            return new BattlePassService(SampleSeason(), cosmetics, new RemoteConfig(),
                premiumOwned, analytics: null, time: clock);
        }

        [Test]
        public void EveryRewardOnBothTracks_IsCosmetic()
        {
            foreach (BattlePassReward reward in SampleSeason())
            {
                Assert.IsTrue(reward.IsCosmetic,
                    $"Reward at tier {reward.Tier} ({reward.Track}) must be cosmetic (§7).");
                Assert.IsFalse(string.IsNullOrEmpty(reward.CosmeticId),
                    "A reward with no cosmetic id could hide a non-cosmetic grant.");
            }
        }

        [Test]
        public void Reward_CannotBeConstructedWithoutACosmetic()
        {
            // The type itself refuses a power grant: no cosmetic id => throws. This is what makes
            // "every reward is cosmetic" structurally true rather than merely conventional.
            Assert.Throws<ArgumentException>(
                () => new BattlePassReward(1, BattlePassTrack.Premium, null));
            Assert.Throws<ArgumentException>(
                () => new BattlePassReward(1, BattlePassTrack.Free, ""));
        }

        [Test]
        public void FreeTrack_IsCompletableF2P()
        {
            BattlePassService pass = BuildPass(out CosmeticState cosmetics, premiumOwned: false);

            // Reach the top tier purely from gameplay progress (no premium ownership).
            pass.AddTierProgress(pass.MaxTier);

            Assert.IsTrue(pass.IsFreeTrackComplete(),
                "The free track must be completable without spending (§7).");

            IReadOnlyList<string> granted = pass.ClaimUnlockedRewards();

            // All five free cosmetics are owned; NO premium cosmetic is granted (not owned).
            for (int tier = 1; tier <= 5; tier++)
            {
                CollectionAssert.Contains(cosmetics.OwnedCosmeticIds, $"free.skin.{tier}");
                CollectionAssert.DoesNotContain(cosmetics.OwnedCosmeticIds, $"prem.border.{tier}");
            }

            Assert.AreEqual(5, granted.Count, "Exactly the five free-track cosmetics.");
        }

        [Test]
        public void PremiumTrack_GrantsOnlyCosmetics_WhenOwned()
        {
            BattlePassService pass = BuildPass(out CosmeticState cosmetics, premiumOwned: true);
            pass.AddTierProgress(pass.MaxTier);

            pass.ClaimUnlockedRewards();

            // Both tracks' cosmetics owned; the collection holds cosmetic ids only.
            Assert.AreEqual(10, cosmetics.OwnedCosmeticIds.Count);
            foreach (string id in cosmetics.OwnedCosmeticIds)
            {
                Assert.IsTrue(id.StartsWith("free.skin.") || id.StartsWith("prem.border."),
                    $"Owned id '{id}' must be one of the season's cosmetics (§7).");
            }
        }

        [Test]
        public void TierSkip_AdvancesTierProgressOnly_NoPowerNoUnitsNoCurrency()
        {
            BattlePassService pass = BuildPass(out CosmeticState cosmetics, premiumOwned: false);

            int before = pass.CurrentTier;
            int after = pass.SkipTier(2);

            // The ONLY effect is tier advance — and never more than the purchased amount.
            Assert.AreEqual(before + 2, after, "Skip advances exactly the purchased tiers.");
            Assert.LessOrEqual(after - before, 2,
                "A tier skip must never advance more tiers than purchased (§7).");

            // No side effects: nothing was granted into cosmetics merely by skipping (claiming is
            // a separate, explicit step), and the service has no roster/wallet handle at all.
            Assert.AreEqual(0, cosmetics.OwnedCosmeticIds.Count,
                "Skipping a tier grants no cosmetic, unit, Stone, or Shard by itself (§7).");
        }

        [Test]
        public void TierSkip_NeverExceedsMaxTier()
        {
            BattlePassService pass = BuildPass(out _, premiumOwned: false);
            pass.SkipTier(100); // far past the 5-tier season
            Assert.AreEqual(pass.MaxTier, pass.CurrentTier, "Skips clamp to the season max.");
        }

        [Test]
        public void MalformedSeasonWithNonCosmeticReward_FailsFastAtConstruction()
        {
            // Defense in depth: even though the reward type guarantees cosmetic-only, a null
            // entry in the list must be rejected so a malformed season cannot ship.
            var bad = new List<BattlePassReward> { null };
            Assert.Throws<ArgumentException>(
                () => new BattlePassService(bad, new CosmeticState(), new RemoteConfig()));
        }
    }
}
