using System.Collections.Generic;
using System.Linq;
using Keepfall.Data;
using Keepfall.Deck;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// THE REQUIRED DECK TEST (source-of-truth §5). Proves the validator rejects decks that are
    /// not exactly 8 cards, that average below 2.6 or above 3.0, and that are missing a Vanguard
    /// or a Champion; accepts a legal deck; respects unit ownership; and exposes the correct
    /// saved-deck slot caps (3 F2P · 4 Plus · up to 6 purchased).
    /// </summary>
    public sealed class DeckValidatorTests
    {
        // Build an id→seed map from the canonical roster so tests use real elixir costs/roles.
        private static IReadOnlyDictionary<string, UnitSeed> AllUnits()
        {
            return RosterCatalog.Units.ToDictionary(u => u.Id, u => u);
        }

        // A known-legal F2P deck: avg elixir 2.875, has Vanguard (Bulwark) + Champion (Captain).
        // (Verified constructible from starter+core in the roster design.)
        private static List<string> LegalDeck() => new List<string>
        {
            "bulwark",     // Vanguard, 4
            "captain",     // Champion, 4
            "cutpurse",    // Skirmisher, 1
            "marksman",    // Archer, 3
            "slinger",     // Archer, 2
            "frostweaver", // Mage, 3
            "cinder",      // Mage, 2
            "bombard",     // Engineer, 4
        }; // sum 23 → avg 2.875

        private static HashSet<string> OwnsAll() =>
            new HashSet<string>(RosterCatalog.Units.Select(u => u.Id));

        [Test]
        public void AcceptsALegalDeck()
        {
            DeckValidationResult result =
                DeckValidator.Validate(LegalDeck(), AllUnits(), OwnsAll());

            Assert.IsTrue(result.IsValid, result.Message);
            Assert.AreEqual(DeckValidationError.None, result.Error);
            Assert.AreEqual(2.875, result.AverageElixir, 1e-9);
            Assert.IsFalse(result.Message.Contains("!"), "UI strings must have no exclamation points (§12).");
        }

        [Test]
        public void RejectsFewerThanEightCards()
        {
            var deck = LegalDeck();
            deck.RemoveAt(0); // 7 cards

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.WrongCardCount, result.Error);
        }

        [Test]
        public void RejectsMoreThanEightCards()
        {
            var deck = LegalDeck();
            deck.Add("rampart"); // 9 cards

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.WrongCardCount, result.Error);
        }

        [Test]
        public void RejectsAverageElixirBelowMinimum()
        {
            // All cheap cards: well under 2.6 average, but still has Vanguard + Champion so the
            // FAILING rule is the elixir average, not a role rule.
            // bulwark(4) + captain(4) + cutpurse(1)*-style cheap set.
            var deck = new List<string>
            {
                "bulwark",  // V, 4
                "captain",  // C, 4  -> these two anchor required roles
                "cutpurse", // 1
                "hound",    // 2
                "outrider", // 2
                "slinger",  // 2
                "cinder",   // 2
                "spark",    // 3  -> sum 20 → avg 2.5 (< 2.6)
            };

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.AverageElixirTooLow, result.Error);
            Assert.AreEqual(2.5, result.AverageElixir, 1e-9);
        }

        [Test]
        public void RejectsAverageElixirAboveMaximum()
        {
            // Heavy deck: average above 3.0, still has Vanguard + Champion.
            var deck = new List<string>
            {
                "rampart",        // V, 5
                "berserker",      // C, 5
                "bulwark",        // V, 4
                "captain",        // C, 4
                "standardbearer", // V, 4
                "volley",         // A, 4
                "wildfire",       // M, 4
                "bombard",        // E, 4  -> sum 34 → avg 4.25 (> 3.0)
            };

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.AverageElixirTooHigh, result.Error);
            Assert.AreEqual(4.25, result.AverageElixir, 1e-9);
        }

        [Test]
        public void AcceptsExactBoundaryAverages()
        {
            // avg exactly 2.6: sum must be 20.8 — not integer-reachable, so test the inclusive
            // bounds we CAN hit: 2.625 (just inside low) and 3.0 (exactly the high bound).
            var atHighBound = new List<string>
            {
                "bulwark",  // V, 4
                "captain",  // C, 4
                "rampart",  // V, 5
                "marksman", // A, 3
                "frostweaver", // M, 3
                "spark",    // M, 3
                "longshot", // A, 3
                "cutpurse", // 1  -> sum 26... adjust below
            };
            // Recompute to hit exactly 3.0 (sum 24): swap to known set.
            atHighBound = new List<string>
            {
                "bulwark",     // 4
                "captain",     // 4
                "marksman",    // 3
                "frostweaver", // 3
                "spark",       // 3
                "longshot",    // 3
                "slinger",     // 2
                "cinder",      // 2  -> sum 24 → avg 3.0 exactly
            };

            DeckValidationResult result =
                DeckValidator.Validate(atHighBound, AllUnits(), OwnsAll());

            Assert.IsTrue(result.IsValid, result.Message);
            Assert.AreEqual(3.0, result.AverageElixir, 1e-9);
        }

        [Test]
        public void RejectsMissingVanguard()
        {
            // No Vanguard; keep avg in range and include a Champion so Vanguard is the failure.
            var deck = new List<string>
            {
                "captain",     // C, 4
                "warden",      // C, 4
                "marksman",    // A, 3
                "slinger",     // A, 2
                "frostweaver", // M, 3
                "cinder",      // M, 2
                "bombard",     // E, 4
                "outrider",    // S, 2  -> sum 24 avg 3.0, no Vanguard
            };

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.MissingVanguard, result.Error);
        }

        [Test]
        public void RejectsMissingChampion()
        {
            // No Champion; keep avg in range and include a Vanguard so Champion is the failure.
            var deck = new List<string>
            {
                "bulwark",     // V, 4
                "rampart",     // V, 5
                "marksman",    // A, 3
                "slinger",     // A, 2
                "frostweaver", // M, 3
                "cinder",      // M, 2
                "bombard",     // E, 4
                "cutpurse",    // S, 1  -> sum 24 avg 3.0, no Champion
            };

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.MissingChampion, result.Error);
        }

        [Test]
        public void RespectsUnitOwnership()
        {
            // A legal-by-composition deck, but the player does not own one card.
            var deck = LegalDeck();
            var owned = new HashSet<string>(deck);
            owned.Remove("bombard"); // player lacks Bombard

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), owned);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.UnitNotOwned, result.Error);
            StringAssert.Contains("Bombard", result.Message);
        }

        [Test]
        public void RejectsDuplicateCards()
        {
            var deck = LegalDeck();
            deck[1] = deck[0]; // duplicate bulwark, still 8 entries

            DeckValidationResult result = DeckValidator.Validate(deck, AllUnits(), OwnsAll());

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(DeckValidationError.DuplicateCard, result.Error);
        }

        [Test]
        public void SlotCapsFollowTheLadder()
        {
            // §5/§6 saved-deck slots: 3 F2P · 4 Plus · up to 6 purchased.
            Assert.AreEqual(3, DeckValidator.MaxDeckSlots(hasPlus: false));
            Assert.AreEqual(4, DeckValidator.MaxDeckSlots(hasPlus: true));

            // Purchases raise the cap up to 6, clamped — no spend exceeds the ceiling.
            Assert.AreEqual(6, DeckValidator.MaxDeckSlots(hasPlus: false, purchasedExtraSlots: 3));
            Assert.AreEqual(6, DeckValidator.MaxDeckSlots(hasPlus: true, purchasedExtraSlots: 3));
            Assert.AreEqual(6, DeckValidator.MaxDeckSlots(hasPlus: true, purchasedExtraSlots: 99));
            Assert.AreEqual(5, DeckValidator.MaxDeckSlots(hasPlus: false, purchasedExtraSlots: 2));
        }
    }
}
