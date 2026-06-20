using System.Collections.Generic;
using System.Linq;
using Keepfall.Data;
using Keepfall.Deck;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Guards the canonical roster shape (source-of-truth §2 ladder, §3 roster): exactly 24
    /// units in a 6/10/6/2 tier split, every role with ≥1 starter and ≥1 specialist, both
    /// masters flagged lateral, Stone-cost bands honored, and proof that at least one legal
    /// (2.6–3.0, Vanguard+Champion) deck is constructible from starter+core units so F2P is
    /// viable.
    /// </summary>
    public sealed class RosterIntegrityTests
    {
        [Test]
        public void ContainsExactlyTwentyFourUnits()
        {
            Assert.AreEqual(24, RosterCatalog.Units.Count);
            Assert.AreEqual(RosterCatalog.TotalUnits, RosterCatalog.Units.Count);
        }

        [Test]
        public void TierSplitIsSixTenSixTwo()
        {
            Assert.AreEqual(6, RosterCatalog.OfTier(UnlockTier.Starter).Count(), "starters");
            Assert.AreEqual(10, RosterCatalog.OfTier(UnlockTier.Core).Count(), "core");
            Assert.AreEqual(6, RosterCatalog.OfTier(UnlockTier.Specialist).Count(), "specialist");
            Assert.AreEqual(2, RosterCatalog.OfTier(UnlockTier.Master).Count(), "master");

            // Constants must agree with the actual seed.
            Assert.AreEqual(RosterCatalog.StarterCount, RosterCatalog.OfTier(UnlockTier.Starter).Count());
            Assert.AreEqual(RosterCatalog.CoreCount, RosterCatalog.OfTier(UnlockTier.Core).Count());
            Assert.AreEqual(RosterCatalog.SpecialistCount, RosterCatalog.OfTier(UnlockTier.Specialist).Count());
            Assert.AreEqual(RosterCatalog.MasterCount, RosterCatalog.OfTier(UnlockTier.Master).Count());
        }

        [Test]
        public void EveryRoleHasAtLeastOneStarterAndOneSpecialist()
        {
            foreach (Role role in System.Enum.GetValues(typeof(Role)).Cast<Role>())
            {
                int starters = RosterCatalog.OfRole(role)
                    .Count(u => u.UnlockTier == UnlockTier.Starter);
                int specialists = RosterCatalog.OfRole(role)
                    .Count(u => u.UnlockTier == UnlockTier.Specialist);

                Assert.GreaterOrEqual(starters, 1, $"{role} must have ≥1 starter (§3).");
                Assert.GreaterOrEqual(specialists, 1, $"{role} must have ≥1 specialist (§3).");
            }
        }

        [Test]
        public void UnitIdsAreUniqueAndNonEmpty()
        {
            var ids = RosterCatalog.Units.Select(u => u.Id).ToList();
            Assert.IsFalse(ids.Any(string.IsNullOrWhiteSpace), "no blank ids");
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "ids must be unique");
        }

        [Test]
        public void NamedSampleUnitsFromSourceOfTruthArePresent()
        {
            // §3 named sample units must all exist by display name.
            string[] required =
            {
                "Bulwark", "Standardbearer", "Hound", "Pathfinder", "Longshot", "Volley",
                "Spark", "Wildfire", "Tower", "Snare", "Captain", "Berserker",
            };
            var names = new HashSet<string>(RosterCatalog.Units.Select(u => u.DisplayName));
            foreach (string n in required)
            {
                Assert.IsTrue(names.Contains(n), $"Named sample unit '{n}' (§3) must be in the roster.");
            }
        }

        [Test]
        public void ExactlyTwoMastersAndBothAreFlaggedLateral()
        {
            var masters = RosterCatalog.OfTier(UnlockTier.Master).ToList();
            Assert.AreEqual(2, masters.Count);
            Assert.IsTrue(masters.All(m => m.IsLateralMaster),
                "Both masters must be flagged lateral — never strict upgrades (§3).");

            // And NO non-master is flagged lateral.
            Assert.IsFalse(
                RosterCatalog.Units.Any(u => u.IsLateralMaster && u.UnlockTier != UnlockTier.Master),
                "Only Master units may carry the lateral flag.");
        }

        [Test]
        public void MastersDoNotDominateLowerTierUnitsOnEveryStat()
        {
            // §3 invariant: a Master must NOT be a strict upgrade. For each master, prove there
            // exists at least one lower-tier unit that beats it on at least one axis (a stat is
            // higher, or the master's elixir cost is not strictly lower). i.e. the master is not
            // pareto-dominant over the whole roster.
            var lowerTiers = RosterCatalog.Units
                .Where(u => u.UnlockTier != UnlockTier.Master)
                .ToList();

            foreach (UnitSeed master in RosterCatalog.OfTier(UnlockTier.Master))
            {
                bool someoneBeatsItSomewhere = lowerTiers.Any(other =>
                    other.Stats.Hp > master.Stats.Hp ||
                    other.Stats.Dmg > master.Stats.Dmg ||
                    other.Stats.Range > master.Stats.Range ||
                    other.Stats.Speed > master.Stats.Speed ||
                    other.ElixirCost < master.ElixirCost);

                Assert.IsTrue(someoneBeatsItSomewhere,
                    $"Master '{master.DisplayName}' must be lateral, not strictly best on every axis (§3).");
            }
        }

        [Test]
        public void StoneCostsRespectLadderBands()
        {
            // §2 unlock ladder bands.
            foreach (UnitSeed u in RosterCatalog.Units)
            {
                switch (u.UnlockTier)
                {
                    case UnlockTier.Starter:
                        Assert.LessOrEqual(u.StoneCost, 150, $"{u.DisplayName} starter ≤150 Stone");
                        Assert.GreaterOrEqual(u.StoneCost, 0, $"{u.DisplayName} starter ≥0 Stone");
                        break;
                    case UnlockTier.Core:
                        Assert.GreaterOrEqual(u.StoneCost, 300, $"{u.DisplayName} core ≥300 Stone");
                        Assert.LessOrEqual(u.StoneCost, 1200, $"{u.DisplayName} core ≤1,200 Stone");
                        break;
                    case UnlockTier.Specialist:
                        Assert.GreaterOrEqual(u.StoneCost, 2500, $"{u.DisplayName} specialist ≥2,500 Stone");
                        Assert.LessOrEqual(u.StoneCost, 6000, $"{u.DisplayName} specialist ≤6,000 Stone");
                        break;
                    case UnlockTier.Master:
                        Assert.GreaterOrEqual(u.StoneCost, 10000, $"{u.DisplayName} master ≥10,000 Stone");
                        Assert.LessOrEqual(u.StoneCost, 15000, $"{u.DisplayName} master ≤15,000 Stone");
                        break;
                }
            }
        }

        [Test]
        public void ElixirCostsAreInRange()
        {
            foreach (UnitSeed u in RosterCatalog.Units)
            {
                Assert.GreaterOrEqual(u.ElixirCost, 1, $"{u.DisplayName} elixir ≥1");
                Assert.LessOrEqual(u.ElixirCost, 10, $"{u.DisplayName} elixir ≤10 (§4 cap)");
            }
        }

        [Test]
        public void StarterUnitIdsMatchTheSeed()
        {
            // The hardcoded starter ids used to seed a fresh save must equal the actual starters.
            var fromSeed = RosterCatalog.OfTier(UnlockTier.Starter)
                .Select(u => u.Id).OrderBy(x => x).ToArray();
            var declared = RosterCatalog.StarterUnitIds.OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(fromSeed, declared);
            Assert.AreEqual(6, declared.Length);
        }

        [Test]
        public void AtLeastOneLegalDeckIsConstructibleFromStarterAndCoreUnits()
        {
            // F2P viability (§3): an F2P player who only has starter + core units must be able to
            // build a legal deck (8 cards, avg 2.6–3.0, ≥1 Vanguard, ≥1 Champion). We construct a
            // concrete one and validate it through the real DeckValidator.
            var f2pPool = RosterCatalog.Units
                .Where(u => u.UnlockTier == UnlockTier.Starter || u.UnlockTier == UnlockTier.Core)
                .ToList();
            var unitsById = RosterCatalog.Units.ToDictionary(u => u.Id, u => u);
            var owned = new HashSet<string>(f2pPool.Select(u => u.Id));

            // A concrete legal deck from the F2P pool (avg 2.875).
            var deck = new List<string>
            {
                "bulwark",     // V, 4
                "captain",     // C, 4
                "cutpurse",    // S, 1
                "marksman",    // A, 3
                "slinger",     // A, 2
                "frostweaver", // M, 3
                "cinder",      // M, 2
                "bombard",     // E, 4
            };

            // Sanity: every card is in the F2P pool.
            Assert.IsTrue(deck.All(owned.Contains), "deck must use only starter/core units");

            DeckValidationResult result = DeckValidator.Validate(deck, unitsById, owned);
            Assert.IsTrue(result.IsValid, $"F2P deck must validate: {result.Message}");
            Assert.GreaterOrEqual(result.AverageElixir, DeckValidator.MinAverageElixir);
            Assert.LessOrEqual(result.AverageElixir, DeckValidator.MaxAverageElixir);
        }
    }
}
