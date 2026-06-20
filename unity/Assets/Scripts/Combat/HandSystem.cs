using System;
using System.Collections.Generic;

namespace Keepfall.Combat
{
    /// <summary>
    /// The in-match hand (source-of-truth §4): a 5-card hand drawn from the 8-card deck, cycling
    /// on play. Clash-Royale-style mechanic: the deck is shuffled once (deterministically from
    /// the match seed) into a queue; the first <see cref="HandSize"/> cards form the hand, and
    /// playing a card sends it to the BACK of the queue and pulls the next one forward. Because
    /// the shuffle and the queue are fully determined by the seed and the order of plays, a
    /// retry that replays the same plays produces the IDENTICAL hand sequence (§6 Product 3:
    /// "identical … starting hand"). Pure C# — no UnityEngine — so it is EditMode-testable.
    /// </summary>
    public sealed class HandSystem
    {
        /// <summary>Cards visible/playable at once (§4).</summary>
        public const int HandSize = 5;

        /// <summary>Deck size the hand cycles through (§4/§5).</summary>
        public const int DeckSize = 8;

        // Queue of unit ids; index 0..HandSize-1 is the current hand, the rest is the draw order.
        private readonly List<string> _queue;

        /// <summary>
        /// Builds the hand from an 8-card deck and a match seed. The deck is copied and shuffled
        /// deterministically (Fisher–Yates driven by <see cref="DeterministicRng"/>) so the same
        /// seed always yields the same opening hand and cycle.
        /// </summary>
        public HandSystem(IReadOnlyList<string> deckUnitIds, ulong matchSeed)
        {
            if (deckUnitIds == null)
            {
                throw new ArgumentNullException(nameof(deckUnitIds));
            }

            if (deckUnitIds.Count != DeckSize)
            {
                throw new ArgumentException(
                    $"Hand requires an 8-card deck; got {deckUnitIds.Count}.", nameof(deckUnitIds));
            }

            _queue = new List<string>(deckUnitIds);

            // Use a derived stream so the hand shuffle is independent of AI/map draws but still
            // reproducible from the root match seed.
            var rng = new DeterministicRng(DeterministicRng.DeriveSeed(matchSeed, HandSalt));
            ShuffleInPlace(_queue, rng);
        }

        // Distinct salt so the hand stream never collides with the AI or map streams.
        private const ulong HandSalt = 0x48414E44_00000001UL; // "HAND"

        /// <summary>The current 5-card hand, in slot order. Index 0 is the oldest playable card.</summary>
        public IReadOnlyList<string> CurrentHand
        {
            get
            {
                var hand = new List<string>(HandSize);
                for (int i = 0; i < HandSize; i++)
                {
                    hand.Add(_queue[i]);
                }

                return hand;
            }
        }

        /// <summary>The card that will rotate into the hand next (the "next card" preview).</summary>
        public string NextCard => _queue[HandSize];

        /// <summary>
        /// Plays the card in hand slot <paramref name="handSlot"/> (0..4): removes it from the
        /// hand, appends it to the back of the cycle, and slides the next card into the hand.
        /// Returns the id of the card that was played. Deterministic given the same call order.
        /// </summary>
        public string Play(int handSlot)
        {
            if (handSlot < 0 || handSlot >= HandSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(handSlot), $"Hand slot must be 0..{HandSize - 1}.");
            }

            string played = _queue[handSlot];
            _queue.RemoveAt(handSlot);
            _queue.Add(played); // back of the cycle — it returns after the others are seen.
            return played;
        }

        /// <summary>
        /// Plays the first hand slot holding <paramref name="unitId"/>. Convenience for an AI or
        /// UI that thinks in unit ids rather than slot indices. Returns false if not in hand.
        /// </summary>
        public bool TryPlayUnit(string unitId)
        {
            for (int i = 0; i < HandSize; i++)
            {
                if (_queue[i] == unitId)
                {
                    Play(i);
                    return true;
                }
            }

            return false;
        }

        private static void ShuffleInPlace(List<string> list, DeterministicRng rng)
        {
            // Fisher–Yates: deterministic given the rng, unbiased over the deck.
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
