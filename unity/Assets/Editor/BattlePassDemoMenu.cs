using System.Collections.Generic;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Monetization;
using UnityEditor;
using UnityEngine;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// Milestone 04 on-ramp (<c>milestone/04-battlepass</c>: "first 30-day season content ready",
    /// source-of-truth §7/§13). Exercises Battle Pass Season 1 ("Sunset Watch") headlessly and
    /// prints to the Console. Proves the §7 promises: both tracks are cosmetic-only, the FREE track
    /// is completable F2P, and the tier-skip grants tier progress only.
    /// <para>Menu: <b>Keepfall ▸ Battle Pass</b>. Logic lives in <see cref="BattlePassService"/>;
    /// content in <see cref="BattlePassSeason1"/>.</para>
    /// </summary>
    public static class BattlePassDemoMenu
    {
        [MenuItem("Keepfall/Battle Pass/Log Season 1 Track")]
        public static void LogTrack()
        {
            Debug.Log($"[BattlePass] Season {BattlePassSeason1.SeasonId} \"{BattlePassSeason1.SeasonName}\" " +
                      $"- {BattlePassSeason1.Tiers} tiers over {BattlePassSeason1.SeasonDays} days.");
            foreach (BattlePassReward r in BattlePassSeason1.Rewards())
            {
                Debug.Log($"[BattlePass]   T{r.Tier,2} {r.Track,-7} {r.CosmeticId}");
            }
        }

        [MenuItem("Keepfall/Battle Pass/Simulate Free-Track Completion (F2P)")]
        public static void SimulateFreeCompletion()
        {
            BattlePassService pass = Build(out CosmeticState cosmetics, premiumOwned: false);
            pass.AddTierProgress(pass.MaxTier); // earned purely from gameplay, no spend
            IReadOnlyList<string> granted = pass.ClaimUnlockedRewards();

            Debug.Log($"[BattlePass] Reached tier {pass.CurrentTier}/{pass.MaxTier}. " +
                      $"Free track complete: {pass.IsFreeTrackComplete()} (no spend).");
            Debug.Log($"[BattlePass] Claimed {granted.Count} free cosmetics: {string.Join(", ", granted)}");
            Debug.Log($"[BattlePass] Owned cosmetics now: {cosmetics.OwnedCosmeticIds.Count} " +
                      "(premium track not owned, so its cosmetics were not granted).");
        }

        [MenuItem("Keepfall/Battle Pass/Simulate Premium Unlock + Claim All")]
        public static void SimulatePremium()
        {
            BattlePassService pass = Build(out CosmeticState cosmetics, premiumOwned: true);
            pass.AddTierProgress(pass.MaxTier);
            pass.ClaimUnlockedRewards();

            Debug.Log($"[BattlePass] Premium owned. Owned cosmetics: {cosmetics.OwnedCosmeticIds.Count} " +
                      "(all 12 = 5 free + 7 premium). Premium is a cosmetic purchase - no combat advantage.");
        }

        private static BattlePassService Build(out CosmeticState cosmetics, bool premiumOwned)
        {
            cosmetics = new CosmeticState();
            return new BattlePassService(
                BattlePassSeason1.Rewards(), cosmetics, new RemoteConfig(),
                premiumOwned, new DebugAnalytics());
        }
    }
}
