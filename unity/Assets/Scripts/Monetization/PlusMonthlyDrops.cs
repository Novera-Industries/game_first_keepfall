using System;
using System.Collections.Generic;

namespace Keepfall.Monetization
{
    /// <summary>
    /// Keepfall Plus monthly cosmetic-drop schedule (source-of-truth §6 Product 2). Canonical
    /// content lives in <c>config/plus-monthly-drops.json</c>; this is its compile-time mirror.
    /// One cosmetic per active subscription month (skin or border), folded into permanent
    /// ownership by <see cref="PlusSubscription.GrantMonthlyCosmetic"/> and KEPT on cancellation
    /// (§6). Every id is a cosmetic — never a unit, currency, tile-as-power, or stat.
    /// <para>12 months are defined; after month 12 the schedule repeats (an already-owned drop is
    /// not re-granted, since <see cref="PlusSubscription.GrantMonthlyCosmetic"/> de-duplicates).</para>
    /// </summary>
    public static class PlusMonthlyDrops
    {
        /// <summary>Number of distinct monthly drops defined (one subscription year).</summary>
        public const int Months = 12;

        /// <summary>
        /// The ordered drop schedule (month 1 first). Must stay identical to
        /// <c>config/plus-monthly-drops.json</c> (parity is checked by tests).
        /// </summary>
        public static IReadOnlyList<string> Schedule()
        {
            return new List<string>
            {
                "cosmetic.plus.01.dawnward_vanguard",
                "cosmetic.plus.02.amber_watch",
                "cosmetic.plus.03.cinder_skirmisher",
                "cosmetic.plus.04.duskline",
                "cosmetic.plus.05.verdant_archer",
                "cosmetic.plus.06.riverlight",
                "cosmetic.plus.07.emberward_mage",
                "cosmetic.plus.08.coppervein",
                "cosmetic.plus.09.gilded_champion",
                "cosmetic.plus.10.nightfall",
                "cosmetic.plus.11.hearthlight_engineer",
                "cosmetic.plus.12.golden_hour",
            };
        }

        /// <summary>
        /// The cosmetic id dropped in <paramref name="monthIndex"/> (1-based: the player's first
        /// active month is 1). Wraps after <see cref="Months"/> so an ongoing subscription always
        /// has a drop to grant. Throws for a non-positive month.
        /// </summary>
        public static string ForMonth(int monthIndex)
        {
            if (monthIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthIndex), "The first active subscription month is 1.");
            }

            IReadOnlyList<string> schedule = Schedule();
            return schedule[(monthIndex - 1) % schedule.Count];
        }
    }
}
