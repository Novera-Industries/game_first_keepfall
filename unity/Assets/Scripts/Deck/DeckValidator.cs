using System.Collections.Generic;
using Keepfall.Data;

namespace Keepfall.Deck
{
    /// <summary>
    /// Pure deck-composition validator (source-of-truth §5). Holds no Unity dependency so it can
    /// run in EditMode tests and be shared by the deck-builder UI and the match bootstrap.
    ///
    /// Rules enforced (§5):
    ///   • Exactly 8 cards.
    ///   • Average elixir cost in [2.6, 3.0] inclusive.
    ///   • At least one Vanguard AND at least one Champion.
    ///   • Every unit must be OWNED by the player.
    ///   • No duplicate cards (a card is a unit; a deck is a set of distinct units).
    ///
    /// Slot rules are about how many SAVED DECKS a player may keep, not cards-per-deck:
    /// 3 (F2P) · 4 (Plus) · up to 6 (purchased expansion) (§5/§6). <see cref="MaxDeckSlots"/>
    /// exposes that ladder for the UI; the per-deck card rules above are independent of it.
    ///
    /// Tone (§12): every failure message is calm, honest, second-person, no exclamation points.
    /// </summary>
    public static class DeckValidator
    {
        /// <summary>Canonical deck size (§5).</summary>
        public const int RequiredCardCount = 8;

        /// <summary>Inclusive lower bound on average elixir cost (§5).</summary>
        public const double MinAverageElixir = 2.6;

        /// <summary>Inclusive upper bound on average elixir cost (§5).</summary>
        public const double MaxAverageElixir = 3.0;

        // ── Deck-slot ladder (§5/§6): how many decks a player may save. ───────────────────
        /// <summary>Saved-deck slots for a free player (§5).</summary>
        public const int F2PDeckSlots = 3;

        /// <summary>Saved-deck slots with Keepfall Plus (+1 slot, §6). Convenience, not power.</summary>
        public const int PlusDeckSlots = 4;

        /// <summary>Maximum saved-deck slots with purchased expansion (§5).</summary>
        public const int MaxPurchasedDeckSlots = 6;

        /// <summary>
        /// Resolves the maximum number of SAVED DECK slots a player may have, given Plus status
        /// and any purchased extra slots (§5/§6). F2P=3, Plus=4, purchasing raises the cap up to
        /// 6. The Plus bonus and purchased extras both count toward the same ceiling; the result
        /// is clamped to <see cref="MaxPurchasedDeckSlots"/> so no spend ever exceeds the cap.
        /// </summary>
        public static int MaxDeckSlots(bool hasPlus, int purchasedExtraSlots = 0)
        {
            if (purchasedExtraSlots < 0)
            {
                purchasedExtraSlots = 0;
            }

            int baseSlots = hasPlus ? PlusDeckSlots : F2PDeckSlots;
            int total = baseSlots + purchasedExtraSlots;
            return total > MaxPurchasedDeckSlots ? MaxPurchasedDeckSlots : total;
        }

        /// <summary>
        /// Validates a deck (list of unit ids) against §5. <paramref name="unitsById"/> resolves
        /// each id to its definition (role + elixir). <paramref name="ownedUnitIds"/> is the set
        /// the player owns. Returns a structured result with the computed average and, on
        /// failure, a single calm message describing the first failed rule.
        /// </summary>
        public static DeckValidationResult Validate(
            IReadOnlyList<string> deckUnitIds,
            IReadOnlyDictionary<string, UnitSeed> unitsById,
            IReadOnlyCollection<string> ownedUnitIds)
        {
            if (deckUnitIds == null)
            {
                return DeckValidationResult.Fail(
                    DeckValidationError.WrongCardCount,
                    "Your deck is empty. Add 8 cards to continue.",
                    0.0,
                    0);
            }

            int count = deckUnitIds.Count;

            // Rule: exactly 8 cards. Reported first so an empty/short deck gives the clearest help.
            if (count != RequiredCardCount)
            {
                return DeckValidationResult.Fail(
                    DeckValidationError.WrongCardCount,
                    $"Your deck has {count} cards. A deck needs exactly {RequiredCardCount}.",
                    0.0,
                    count);
            }

            // Rule: no duplicate cards. A deck is a set of distinct units.
            var seen = new HashSet<string>();
            foreach (string id in deckUnitIds)
            {
                if (!seen.Add(id))
                {
                    string dupName = ResolveName(id, unitsById);
                    return DeckValidationResult.Fail(
                        DeckValidationError.DuplicateCard,
                        $"{dupName} appears more than once. Each card can be used only once.",
                        0.0,
                        count);
                }
            }

            // Rule: every card must resolve to a known unit (guards stale/edited saves).
            foreach (string id in deckUnitIds)
            {
                if (!unitsById.ContainsKey(id))
                {
                    return DeckValidationResult.Fail(
                        DeckValidationError.UnknownUnit,
                        "One of your cards is no longer available. Replace it to continue.",
                        0.0,
                        count);
                }
            }

            // Rule: every card must be owned (§5 "all units owned").
            foreach (string id in deckUnitIds)
            {
                if (!ownedUnitIds.Contains(id))
                {
                    string name = ResolveName(id, unitsById);
                    return DeckValidationResult.Fail(
                        DeckValidationError.UnitNotOwned,
                        $"You do not own {name} yet. Unlock it with Stone, then add it.",
                        0.0,
                        count);
                }
            }

            // Compute average elixir and role presence in a single pass.
            int elixirSum = 0;
            bool hasVanguard = false;
            bool hasChampion = false;
            foreach (string id in deckUnitIds)
            {
                UnitSeed unit = unitsById[id];
                elixirSum += unit.ElixirCost;
                if (unit.Role == Role.Vanguard)
                {
                    hasVanguard = true;
                }
                else if (unit.Role == Role.Champion)
                {
                    hasChampion = true;
                }
            }

            double average = (double)elixirSum / RequiredCardCount;

            // Rule: at least one Vanguard (§5).
            if (!hasVanguard)
            {
                return DeckValidationResult.Fail(
                    DeckValidationError.MissingVanguard,
                    "Your deck needs at least one Vanguard to hold the front.",
                    average,
                    count);
            }

            // Rule: at least one Champion (§5).
            if (!hasChampion)
            {
                return DeckValidationResult.Fail(
                    DeckValidationError.MissingChampion,
                    "Your deck needs at least one Champion as a win condition.",
                    average,
                    count);
            }

            // Rule: average elixir in [2.6, 3.0] inclusive (§5). Use a small epsilon so values
            // that are exactly on the bound (e.g. 2.6, 3.0) are accepted despite float drift.
            const double epsilon = 1e-9;
            if (average < MinAverageElixir - epsilon)
            {
                return DeckValidationResult.Fail(
                    DeckValidationError.AverageElixirTooLow,
                    $"Your average elixir is {average:0.00}. Add a heavier card to reach {MinAverageElixir:0.0}.",
                    average,
                    count);
            }

            if (average > MaxAverageElixir + epsilon)
            {
                return DeckValidationResult.Fail(
                    DeckValidationError.AverageElixirTooHigh,
                    $"Your average elixir is {average:0.00}. Swap in a lighter card to reach {MaxAverageElixir:0.0}.",
                    average,
                    count);
            }

            return DeckValidationResult.Ok(average);
        }

        private static string ResolveName(string id, IReadOnlyDictionary<string, UnitSeed> unitsById)
        {
            return unitsById != null && unitsById.TryGetValue(id, out UnitSeed seed)
                ? seed.DisplayName
                : id;
        }
    }
}
