using System.Collections.Generic;

namespace Keepfall.Monetization
{
    /// <summary>
    /// Battle Pass Season 1 — "Sunset Watch" (source-of-truth §7). The canonical content lives in
    /// <c>config/battlepass-season1.json</c>; this is its compile-time mirror for the client. 30
    /// tiers over 30 days, 12 cosmetics = 5 free + 7 premium (within the Part B 8–12 SKU brief).
    /// Reaching tier 30 completes the FREE track with no spend.
    /// <para>Every reward is a cosmetic by construction (<see cref="BattlePassReward"/> has no
    /// non-cosmetic payload). The display names + asset types are the art-pipeline contract and
    /// live in the JSON; the client only needs tier/track/cosmeticId.</para>
    /// </summary>
    public static class BattlePassSeason1
    {
        /// <summary>Stable season id (analytics + save keys).</summary>
        public const string SeasonId = "s1";

        /// <summary>Human-facing season name.</summary>
        public const string SeasonName = "Sunset Watch";

        /// <summary>Tier count / season length in days (both 30, §7).</summary>
        public const int Tiers = 30;
        public const int SeasonDays = 30;

        /// <summary>
        /// The Season 1 reward track. Order is by tier; tiers without an entry are pure
        /// progression. Must stay identical to <c>config/battlepass-season1.json</c>
        /// (parity is checked by <c>BattlePassSeason1Tests</c>).
        /// </summary>
        public static IReadOnlyList<BattlePassReward> Rewards()
        {
            return new List<BattlePassReward>
            {
                new BattlePassReward(1,  BattlePassTrack.Premium, "cosmetic.s1.banner.sunset_standardbearer"),
                new BattlePassReward(3,  BattlePassTrack.Free,    "cosmetic.s1.border.first_light"),
                new BattlePassReward(6,  BattlePassTrack.Premium, "cosmetic.s1.skin.ember_mage"),
                new BattlePassReward(8,  BattlePassTrack.Free,    "cosmetic.s1.banner.dusk"),
                new BattlePassReward(12, BattlePassTrack.Premium, "cosmetic.s1.skin.dusk_vanguard"),
                new BattlePassReward(15, BattlePassTrack.Free,    "cosmetic.s1.emote.quiet_victory"),
                new BattlePassReward(18, BattlePassTrack.Premium, "cosmetic.s1.banner.riverkeep"),
                new BattlePassReward(22, BattlePassTrack.Free,    "cosmetic.s1.border.wayfarer"),
                new BattlePassReward(24, BattlePassTrack.Premium, "cosmetic.s1.border.gilded_hoard"),
                new BattlePassReward(28, BattlePassTrack.Premium, "cosmetic.s1.emote.marshals_salute"),
                new BattlePassReward(30, BattlePassTrack.Free,    "cosmetic.s1.tile.lone_tile"),
                new BattlePassReward(30, BattlePassTrack.Premium, "cosmetic.s1.tile.golden_hour_fortress"),
            };
        }
    }
}
