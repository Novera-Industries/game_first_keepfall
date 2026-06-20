using System;
using Keepfall.Core.Config;
using Keepfall.Core.State;

namespace Keepfall.Economy
{
    /// <summary>
    /// Pure tile-yield math (source-of-truth §2 + §6 Product 2). Stateless and engine-free so
    /// it is trivially unit-testable and identical in EditMode, on device, and (conceptually)
    /// on the backend.
    /// <para>
    /// Core formula: <c>accrual = clamp(ratePerHour * hoursElapsed, 0, cap)</c>. Rate and cap
    /// come from <see cref="RemoteConfig"/> per <see cref="TileRank"/> (canonical defaults
    /// T1 10/120, T2 25/300, T3 60/720 — each fills its cap in 12 hours). Keepfall Plus adds
    /// <b>+50% yield</b> on all owned tiles (§6): we scale BOTH the rate and the cap by the
    /// configured multiplier (default 1.5). Scaling both — not just the rate — is what makes a
    /// claim yield 50% more Stone, which is what shortens roster completion from ~40 days to
    /// ~22 days as §6 promises (scaling rate alone would only fill the same cap faster, leaving
    /// total throughput unchanged).
    /// </para>
    /// <para>
    /// Accrual is measured from <see cref="TileState.LastAccrualUtc"/> to "now", so it is
    /// computed from a wall-clock delta and therefore <b>survives app close</b>: on resume the
    /// game recomputes against the persisted anchor and clamps to the cap. The +50% perk is
    /// NOT a combat advantage and NOT a permanent stat boost — it only compresses time the
    /// player already earned (§6, §10), and it lapses with the subscription.
    /// </para>
    /// </summary>
    public static class TileYield
    {
        /// <summary>
        /// Effective per-hour Stone rate for <paramref name="rank"/>, including the Keepfall
        /// Plus +50% multiplier when <paramref name="subscriptionActive"/> is true.
        /// </summary>
        public static double RatePerHour(RemoteConfig config, TileRank rank, bool subscriptionActive)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            double baseRate = config.GetTileYieldPerHour(rank);
            return subscriptionActive ? baseRate * config.GetPlusYieldMultiplier() : baseRate;
        }

        /// <summary>
        /// Effective pre-claim Stone cap for <paramref name="rank"/>, including the Keepfall
        /// Plus +50% multiplier when <paramref name="subscriptionActive"/> is true. The base
        /// cap is the §2 holding cap; scaling it with the rate keeps the 12-hour fill time
        /// constant while raising per-claim throughput by the perk amount.
        /// </summary>
        public static long Cap(RemoteConfig config, TileRank rank, bool subscriptionActive)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            long baseCap = config.GetTileCap(rank);
            if (!subscriptionActive)
            {
                return baseCap;
            }

            // Round to the nearest whole Stone so the cap stays an integer balance.
            double scaled = baseCap * config.GetPlusYieldMultiplier();
            return (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Computes the Stone a tile should hold given an elapsed wall-clock span, starting from
        /// <paramref name="alreadyAccrued"/>. Negative spans contribute nothing (a clock that
        /// appears to move backwards never destroys earned Stone); the result is clamped to the
        /// effective cap. This is the single accrual primitive every caller routes through.
        /// </summary>
        /// <param name="config">Remote config supplying rate and cap.</param>
        /// <param name="rank">Tile rank.</param>
        /// <param name="subscriptionActive">Whether Keepfall Plus is active.</param>
        /// <param name="alreadyAccrued">Stone already held before this span (e.g. from save).</param>
        /// <param name="elapsed">Wall-clock time since the last accrual anchor.</param>
        public static long Accrue(
            RemoteConfig config,
            TileRank rank,
            bool subscriptionActive,
            long alreadyAccrued,
            TimeSpan elapsed)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (alreadyAccrued < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alreadyAccrued), "Accrued Stone cannot be negative.");
            }

            long cap = Cap(config, rank, subscriptionActive);

            // Already at or above cap: clamp down (e.g. cap shrank when a subscription lapsed)
            // but never add more.
            if (alreadyAccrued >= cap)
            {
                return cap;
            }

            double hours = elapsed.TotalHours;
            if (hours <= 0)
            {
                return alreadyAccrued;
            }

            double gained = RatePerHour(config, rank, subscriptionActive) * hours;
            if (gained <= 0)
            {
                return alreadyAccrued;
            }

            // Floor partial Stone: a tile holds whole Stone, and flooring means we never credit
            // a fraction the player has not fully earned within the elapsed window.
            long total = alreadyAccrued + (long)Math.Floor(gained);
            return total > cap ? cap : total;
        }

        /// <summary>
        /// Convenience overload computing accrual from a tile's persisted anchor to
        /// <paramref name="now"/>, using the tile's current <see cref="TileState.AccruedStone"/>
        /// as the starting balance. Returns the value the tile <i>should</i> hold; it does not
        /// mutate the tile (see <see cref="TileService.RefreshAccrual"/> for the mutating path).
        /// </summary>
        public static long AccrueTo(
            RemoteConfig config,
            TileState tile,
            bool subscriptionActive,
            DateTimeOffset now)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            return Accrue(
                config,
                tile.Rank,
                subscriptionActive,
                tile.AccruedStone,
                now - tile.LastAccrualUtc);
        }
    }
}
