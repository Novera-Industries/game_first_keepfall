using System.Collections.Generic;
using Keepfall.Core.Config;

namespace Keepfall.Combat
{
    /// <summary>
    /// A single AI decision: which card to play (by hand slot) into which lane, or "wait". The
    /// match loop applies this to the AI's <see cref="HandSystem"/> and <see cref="ElixirSystem"/>.
    /// </summary>
    public readonly struct AiDecision
    {
        /// <summary>True when the AI chose to act this step; false means hold elixir.</summary>
        public readonly bool ShouldPlay;

        /// <summary>Hand slot (0..4) to play when <see cref="ShouldPlay"/> is true.</summary>
        public readonly int HandSlot;

        /// <summary>Target lane (0..2).</summary>
        public readonly int Lane;

        /// <summary>Creates a decision.</summary>
        public AiDecision(bool shouldPlay, int handSlot, int lane)
        {
            ShouldPlay = shouldPlay;
            HandSlot = handSlot;
            Lane = lane;
        }

        /// <summary>The "hold elixir this step" decision.</summary>
        public static AiDecision Wait => new AiDecision(false, -1, -1);
    }

    /// <summary>
    /// The PvE opponent (source-of-truth §4). Phase 1 is single-player PvE only (§0); this is the
    /// ONLY opponent in the game — there is no human enemy and no PvP code here (PvP is Phase 2,
    /// represented solely by the inert <c>Keepfall.Core.Pvp.PvpPlaceholder</c>).
    ///
    /// DIFFICULTY SELECTION KEYS OFF ROSTER EXPANSION, NEVER RAW DAYS (§4). <see cref="SelectTier"/>
    /// maps the player's UNLOCKED-UNIT COUNT against the RemoteConfig thresholds
    /// (<c>ai.threshold.apprentice/adept/tactician/commander/marshal</c>). Tuning the curve is a
    /// config push, not a rebuild (§11). The day ranges in §4 are the EXPECTED correlation only.
    ///
    /// Behavior is driven by a deterministic stream derived from the match seed, so the AI plays
    /// IDENTICALLY on a retry (§6 Product 3: "identical AI … "). Higher tiers wait less (better
    /// elixir efficiency) and mis-target less — the knobs are tuned toward the loss-rate targets
    /// documented on <see cref="AiTier"/> (≤25% Apprentice · ~40% Tactician · ~55% Marshal).
    /// </summary>
    public sealed class AiController
    {
        // Distinct salt so the AI's stream never collides with the hand or map streams.
        private const ulong AiSalt = 0x41495F5F_00000002UL; // "AI__"

        private readonly AiTier _tier;
        private readonly DeterministicRng _rng;
        private readonly double _aggression; // probability of acting when elixir allows, per tier
        private readonly double _misplayChance; // probability of a sub-optimal lane choice, per tier

        /// <summary>The tier this controller plays at.</summary>
        public AiTier Tier => _tier;

        /// <summary>Creates an AI at <paramref name="tier"/>, seeded from the match seed.</summary>
        public AiController(AiTier tier, ulong matchSeed)
        {
            _tier = tier;
            _rng = new DeterministicRng(DeterministicRng.DeriveSeed(matchSeed, AiSalt));

            // Tier knobs tuned toward the §4 loss-rate intent. Lower tiers act less efficiently
            // (hoard or dump elixir) and mis-target more often; higher tiers approach optimal.
            switch (tier)
            {
                case AiTier.Apprentice:
                    _aggression = 0.45; _misplayChance = 0.40; break;
                case AiTier.Adept:
                    _aggression = 0.60; _misplayChance = 0.28; break;
                case AiTier.Tactician:
                    _aggression = 0.72; _misplayChance = 0.18; break;
                case AiTier.Commander:
                    _aggression = 0.85; _misplayChance = 0.10; break;
                case AiTier.Marshal:
                default:
                    _aggression = 0.95; _misplayChance = 0.04; break;
            }
        }

        /// <summary>
        /// Selects the AI tier from the player's UNLOCKED-UNIT COUNT against RemoteConfig
        /// thresholds (§4 — roster expansion drives difficulty, never days). The highest tier
        /// whose threshold the count meets or exceeds is chosen. Defaults mirror the bundled
        /// config (apprentice 0, adept 8, tactician 12, commander 18, marshal 22).
        /// </summary>
        public static AiTier SelectTier(int unlockedUnitCount, RemoteConfig config)
        {
            int apprentice = config?.GetInt(RemoteConfigKeys.AiThresholdApprentice, 0) ?? 0;
            int adept = config?.GetInt(RemoteConfigKeys.AiThresholdAdept, 8) ?? 8;
            int tactician = config?.GetInt(RemoteConfigKeys.AiThresholdTactician, 12) ?? 12;
            int commander = config?.GetInt(RemoteConfigKeys.AiThresholdCommander, 18) ?? 18;
            int marshal = config?.GetInt(RemoteConfigKeys.AiThresholdMarshal, 22) ?? 22;

            if (unlockedUnitCount >= marshal)
            {
                return AiTier.Marshal;
            }

            if (unlockedUnitCount >= commander)
            {
                return AiTier.Commander;
            }

            if (unlockedUnitCount >= tactician)
            {
                return AiTier.Tactician;
            }

            if (unlockedUnitCount >= adept)
            {
                return AiTier.Adept;
            }

            // At or below the apprentice threshold (and as the floor) → Apprentice.
            return AiTier.Apprentice;
        }

        /// <summary>
        /// Decides the AI's action for one step given how much elixir it has and the elixir cost
        /// of each card currently in its hand (slot-aligned). Deterministic given the seed and
        /// the call sequence. The AI plays the cheapest affordable card it rolls to act on,
        /// occasionally mis-targeting a lane per its tier's <c>misplayChance</c>.
        /// </summary>
        public AiDecision Decide(int availableElixir, IReadOnlyList<int> handElixirCosts)
        {
            if (handElixirCosts == null || handElixirCosts.Count == 0)
            {
                return AiDecision.Wait;
            }

            // Roll whether to act at all this step (lower tiers hold/misuse elixir more).
            if (_rng.NextDouble() > _aggression)
            {
                return AiDecision.Wait;
            }

            // Find the cheapest affordable card; prefer spending elixir efficiently.
            int bestSlot = -1;
            int bestCost = int.MaxValue;
            for (int i = 0; i < handElixirCosts.Count; i++)
            {
                int cost = handElixirCosts[i];
                if (cost <= availableElixir && cost < bestCost)
                {
                    bestSlot = i;
                    bestCost = cost;
                }
            }

            if (bestSlot < 0)
            {
                return AiDecision.Wait; // nothing affordable yet.
            }

            // Lane choice: optimal AI pressures the lane where it is already winning (most damage
            // dealt); a mis-targeting roll instead picks a random lane. The match loop supplies no
            // board read here, so we model "optimal" as the center lane (the contested default)
            // and mis-targets as a random lane — higher tiers mis-target far less.
            int lane = MatchStateCenterLane;
            if (_rng.NextDouble() < _misplayChance)
            {
                lane = _rng.NextInt(MatchState.TowersPerSide);
            }

            return new AiDecision(true, bestSlot, lane);
        }

        // The contested default lane an efficient AI commits to absent a richer board read.
        private const int MatchStateCenterLane = 1;
    }
}
