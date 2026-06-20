using System.Collections.Generic;
using System.Linq;
using Keepfall.Combat;
using Keepfall.Core.Config;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Proves retry replay is honest (source-of-truth §4 determinism, §6 Product 3: a retry
    /// restores "identical AI, map seed, and starting hand"). The SAME match seed must produce
    /// the SAME hand sequence, the SAME AI decisions, and — fed the same plays — the SAME
    /// resolution. A DIFFERENT seed must (very probably) diverge, so we are testing real
    /// seed-dependence and not a constant.
    /// </summary>
    public sealed class MatchDeterminismTests
    {
        private static List<string> Deck() => new List<string>
        {
            "bulwark", "captain", "cutpurse", "marksman",
            "slinger", "frostweaver", "cinder", "bombard",
        };

        // Replays the same N plays from slot 0 and records the played-card sequence.
        private static List<string> PlaySequence(ulong seed, int plays)
        {
            var hand = new HandSystem(Deck(), seed);
            var seq = new List<string>(plays);
            for (int i = 0; i < plays; i++)
            {
                seq.Add(hand.Play(0));
            }

            return seq;
        }

        [Test]
        public void SameSeedProducesIdenticalHandSequence()
        {
            const ulong seed = 0xC0FFEE123456UL;
            List<string> a = PlaySequence(seed, 24);
            List<string> b = PlaySequence(seed, 24);

            CollectionAssert.AreEqual(a, b, "Same seed must replay the identical hand cycle.");
        }

        [Test]
        public void SameSeedProducesIdenticalOpeningHand()
        {
            const ulong seed = 7777UL;
            var h1 = new HandSystem(Deck(), seed);
            var h2 = new HandSystem(Deck(), seed);

            CollectionAssert.AreEqual(h1.CurrentHand.ToList(), h2.CurrentHand.ToList());
            Assert.AreEqual(h1.NextCard, h2.NextCard);
        }

        [Test]
        public void DifferentSeedsDivergeAtLeastSometimes()
        {
            // Over a handful of distinct seeds, at least one opening hand must differ from seed 1.
            List<string> baseHand = new HandSystem(Deck(), 1UL).CurrentHand.ToList();
            bool anyDifferent = false;
            for (ulong s = 2; s <= 12; s++)
            {
                var other = new HandSystem(Deck(), s).CurrentHand.ToList();
                if (!baseHand.SequenceEqual(other))
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent, "Hand must actually depend on the seed.");
        }

        [Test]
        public void HandCyclePreservesTheEightDeckCards()
        {
            // Every card returns; the cycle is a permutation, never losing or duplicating a card.
            var hand = new HandSystem(Deck(), 42UL);
            var seen = new List<string>();
            for (int i = 0; i < HandSystem.DeckSize; i++)
            {
                seen.Add(hand.Play(0));
            }

            CollectionAssert.AreEquivalent(Deck(), seen,
                "Playing 8 times from slot 0 cycles through all 8 distinct deck cards once.");
        }

        [Test]
        public void SameSeedProducesIdenticalAiDecisions()
        {
            const ulong seed = 0xBADC0DEUL;
            var costs = new List<int> { 4, 4, 1, 3, 2 };

            var ai1 = new AiController(AiTier.Tactician, seed);
            var ai2 = new AiController(AiTier.Tactician, seed);

            for (int step = 0; step < 50; step++)
            {
                AiDecision d1 = ai1.Decide(availableElixir: 6, handElixirCosts: costs);
                AiDecision d2 = ai2.Decide(availableElixir: 6, handElixirCosts: costs);

                Assert.AreEqual(d1.ShouldPlay, d2.ShouldPlay, $"step {step} play flag");
                Assert.AreEqual(d1.HandSlot, d2.HandSlot, $"step {step} slot");
                Assert.AreEqual(d1.Lane, d2.Lane, $"step {step} lane");
            }
        }

        [Test]
        public void SameSeedAndSamePlaysResolveIdentically()
        {
            // Simulate two matches with the same seed and the same scripted tower damage, then
            // confirm the resolver agrees — the retry-replay guarantee end to end.
            MatchOutcome RunScriptedMatch(ulong seed)
            {
                var state = new MatchState(seed, towerHp: 1000);
                // Scripted, seed-independent plays here represent identical player+AI actions on a
                // retry: knock down 2 enemy towers in lanes 0 and 1.
                state.EnemyTowers[0].ApplyDamage(1000);
                state.EnemyTowers[1].ApplyDamage(1000);
                state.Tick(40.0);
                return MatchResolver.Resolve(state);
            }

            Assert.AreEqual(RunScriptedMatch(555UL), RunScriptedMatch(555UL));
            Assert.AreEqual(MatchOutcome.Win, RunScriptedMatch(555UL),
                "Destroying 2 of 3 enemy towers is a decisive win (§4).");
        }

        [Test]
        public void TierSelectionKeysOffRosterExpansionNotDays()
        {
            // §4: difficulty advances on roster expansion (unlocked unit count), never days.
            var config = new RemoteConfig(); // canonical fallbacks: 0/8/12/18/22

            Assert.AreEqual(AiTier.Apprentice, AiController.SelectTier(0, config));
            Assert.AreEqual(AiTier.Apprentice, AiController.SelectTier(7, config));
            Assert.AreEqual(AiTier.Adept, AiController.SelectTier(8, config));
            Assert.AreEqual(AiTier.Tactician, AiController.SelectTier(12, config));
            Assert.AreEqual(AiTier.Commander, AiController.SelectTier(18, config));
            Assert.AreEqual(AiTier.Marshal, AiController.SelectTier(22, config));
            Assert.AreEqual(AiTier.Marshal, AiController.SelectTier(24, config), "full roster → Marshal");
        }

        [Test]
        public void MatchClockClampsToThreeMinutes()
        {
            var state = new MatchState(1UL);
            state.Tick(500.0);
            Assert.AreEqual(MatchState.MatchDurationSeconds, state.ElapsedSeconds, 1e-9);
            Assert.IsTrue(state.TimeExpired);
        }

        [Test]
        public void DamageTiebreakDecidesAtTimeExpiry()
        {
            // Neither side reaches 2 towers; clock expires; most damage wins (§4).
            var state = new MatchState(9UL, towerHp: 1000);
            state.EnemyTowers[0].ApplyDamage(600); // player dealt 600
            state.PlayerTowers[0].ApplyDamage(400); // enemy dealt 400
            state.Tick(MatchState.MatchDurationSeconds);

            Assert.AreEqual(MatchOutcome.Win, MatchResolver.Resolve(state));
        }
    }
}
