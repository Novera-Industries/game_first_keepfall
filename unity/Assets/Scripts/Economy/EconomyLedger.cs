using System;
using System.Collections.Generic;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Data;

namespace Keepfall.Economy
{
    /// <summary>
    /// Unit-unlock ledger (source-of-truth §2 cost ladder, §3 roster). Spends <b>Stone only</b>
    /// against the <see cref="Wallet"/> and records ownership + the price paid into
    /// <see cref="RosterState"/>.
    /// <para>
    /// Two non-negotiable invariants live here:
    /// <list type="bullet">
    ///   <item><b>No unit is ever bought with money.</b> There is no Shard code path in this
    ///   class at all — units are gated by earned Stone, never by premium currency (§10.2). The
    ///   currency parameter is fixed to <see cref="CurrencyType.Stone"/> internally; a Shard
    ///   unlock is unrepresentable, not merely rejected.</item>
    ///   <item><b>Costs trace to the §2 ladder.</b> A requested cost is validated against the
    ///   canonical Stone band for the unit's <see cref="UnlockTier"/> before any spend, so a
    ///   caller cannot smuggle an off-ladder price past the wallet.</item>
    /// </list>
    /// </para>
    /// Pure C# (no UnityEngine) so it is fully EditMode-testable.
    /// </summary>
    public sealed class EconomyLedger
    {
        private readonly Wallet _wallet;
        private readonly RosterState _roster;
        private readonly IAnalytics _analytics;

        /// <summary>
        /// Canonical Stone cost bands per tier (source-of-truth §2 unlock cost ladder). A unit's
        /// price must fall within its tier's inclusive band. Starter's lower bound is 0 to allow
        /// the free starters seeded at install (§2: "Free / 50–150").
        /// </summary>
        private static readonly IReadOnlyDictionary<UnlockTier, (long Min, long Max)> CostBands =
            new Dictionary<UnlockTier, (long, long)>
            {
                [UnlockTier.Starter] = (0, 150),
                [UnlockTier.Core] = (300, 1200),
                [UnlockTier.Specialist] = (2500, 6000),
                [UnlockTier.Master] = (10000, 15000),
            };

        /// <summary>Wires the ledger to the live wallet and roster. Analytics is optional.</summary>
        public EconomyLedger(Wallet wallet, RosterState roster, IAnalytics analytics = null)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _analytics = analytics;
        }

        /// <summary>True if <paramref name="unitId"/> is already unlocked.</summary>
        public bool IsUnlocked(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                throw new ArgumentException("Unit id is required.", nameof(unitId));
            }

            return _roster.UnlockedUnitIds.Contains(unitId);
        }

        /// <summary>The inclusive canonical Stone cost band for a tier (§2).</summary>
        public static (long Min, long Max) CostBandFor(UnlockTier tier)
        {
            if (!CostBands.TryGetValue(tier, out (long Min, long Max) band))
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown unlock tier.");
            }

            return band;
        }

        /// <summary>
        /// Whether <paramref name="stoneCost"/> is a legal price for <paramref name="tier"/>
        /// under the §2 ladder. Off-ladder prices are refused before any spend so tuning stays
        /// honest.
        /// </summary>
        public static bool IsCostWithinBand(UnlockTier tier, long stoneCost)
        {
            (long min, long max) = CostBandFor(tier);
            return stoneCost >= min && stoneCost <= max;
        }

        /// <summary>
        /// Seeds a free starter unit at install (source-of-truth §2: starters are "Free", §8 D1:
        /// "6 starters"). Records a 0-Stone unlock without touching the wallet. No-ops if already
        /// owned. Only valid for <see cref="UnlockTier.Starter"/>.
        /// </summary>
        public UnlockResult GrantStarterUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                throw new ArgumentException("Unit id is required.", nameof(unitId));
            }

            if (IsUnlocked(unitId))
            {
                return UnlockResult.AlreadyOwned(unitId);
            }

            RecordUnlock(unitId, UnlockTier.Starter, 0);
            return new UnlockResult(true, unitId, 0,
                $"You unlocked {unitId}. It is ready for your roster.");
        }

        /// <summary>
        /// Attempts to unlock <paramref name="unitId"/> for <paramref name="stoneCost"/> Stone.
        /// Refuses (changing nothing) if: the unit is already owned, the cost is off the §2 ladder
        /// for <paramref name="tier"/>, or the wallet cannot afford it. On success, debits Stone,
        /// records ownership and the price paid, and emits <see cref="Events.UnitUnlocked"/>.
        /// <para>
        /// There is intentionally no overload that accepts a <see cref="CurrencyType"/>: units are
        /// Stone-only, so spending Shards on a unit is not expressible through this API (§10.2).
        /// </para>
        /// </summary>
        public UnlockResult UnlockUnit(string unitId, UnlockTier tier, long stoneCost)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                throw new ArgumentException("Unit id is required.", nameof(unitId));
            }

            if (stoneCost < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stoneCost), "Unlock cost cannot be negative.");
            }

            if (IsUnlocked(unitId))
            {
                return UnlockResult.AlreadyOwned(unitId);
            }

            if (!IsCostWithinBand(tier, stoneCost))
            {
                (long min, long max) = CostBandFor(tier);
                return UnlockResult.Refused(unitId,
                    $"Cost {stoneCost} Stone is outside the {tier} band ({min}-{max} Stone).");
            }

            // Stone ONLY. No branch accepts Shards — a unit can never be gated by money (§10.2).
            if (!_wallet.TrySpend(CurrencyType.Stone, stoneCost))
            {
                long have = _wallet.GetBalance(CurrencyType.Stone);
                return UnlockResult.Refused(unitId,
                    $"You need {stoneCost} Stone to unlock {unitId}. You have {have}.");
            }

            RecordUnlock(unitId, tier, stoneCost);

            return new UnlockResult(true, unitId, stoneCost,
                $"You unlocked {unitId} for {stoneCost} Stone.");
        }

        private void RecordUnlock(string unitId, UnlockTier tier, long stoneCost)
        {
            _roster.UnlockedUnitIds.Add(unitId);
            _roster.StoneSpentLedger[unitId] = stoneCost;

            _analytics?.Track(Events.UnitUnlocked, new Dictionary<string, object>
            {
                ["unit_id"] = unitId,
                ["tier"] = tier.ToString(),
                ["stone_cost"] = stoneCost,
                // Currency is asserted, not parameterized: units are always bought with Stone.
                ["currency"] = CurrencyType.Stone.ToString(),
            });
        }
    }

    /// <summary>
    /// Outcome of an unlock attempt. Data-only and calm: success carries the price paid and a
    /// quiet confirmation; refusal carries an honest reason with no upsell (source-of-truth §12).
    /// </summary>
    public readonly struct UnlockResult
    {
        /// <summary>True when the unit is now owned (newly unlocked or already owned).</summary>
        public readonly bool Success;

        /// <summary>The unit id the attempt targeted.</summary>
        public readonly string UnitId;

        /// <summary>Stone debited by this call (0 for free starters or an already-owned no-op).</summary>
        public readonly long StoneSpent;

        /// <summary>Calm second-person copy explaining the outcome.</summary>
        public readonly string Message;

        /// <summary>Creates an unlock result.</summary>
        public UnlockResult(bool success, string unitId, long stoneSpent, string message)
        {
            Success = success;
            UnitId = unitId;
            StoneSpent = stoneSpent;
            Message = message;
        }

        /// <summary>A no-op success for a unit the player already owns.</summary>
        public static UnlockResult AlreadyOwned(string unitId) =>
            new UnlockResult(true, unitId, 0, $"You already own {unitId}.");

        /// <summary>A refusal that changed nothing, with an honest reason.</summary>
        public static UnlockResult Refused(string unitId, string reason) =>
            new UnlockResult(false, unitId, 0, reason);
    }
}
