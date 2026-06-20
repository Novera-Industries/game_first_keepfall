namespace Keepfall.Deck
{
    /// <summary>
    /// Machine-readable reason a deck failed validation (source-of-truth §5). The UI maps these
    /// to highlights (e.g. flash the elixir bar) while showing the result's calm message.
    /// </summary>
    public enum DeckValidationError
    {
        /// <summary>No failure.</summary>
        None = 0,

        /// <summary>Card count != 8 (§5).</summary>
        WrongCardCount = 1,

        /// <summary>A unit id appears more than once.</summary>
        DuplicateCard = 2,

        /// <summary>A card id does not resolve to a known unit.</summary>
        UnknownUnit = 3,

        /// <summary>A card is not owned by the player (§5).</summary>
        UnitNotOwned = 4,

        /// <summary>No Vanguard present (§5).</summary>
        MissingVanguard = 5,

        /// <summary>No Champion present (§5).</summary>
        MissingChampion = 6,

        /// <summary>Average elixir below 2.6 (§5).</summary>
        AverageElixirTooLow = 7,

        /// <summary>Average elixir above 3.0 (§5).</summary>
        AverageElixirTooHigh = 8,
    }

    /// <summary>
    /// Structured outcome of <see cref="DeckValidator.Validate"/>. Carries validity, the first
    /// failed rule (if any), a single calm second-person message suitable for direct display
    /// (§12 tone — no exclamation points), the computed average elixir, and the card count.
    /// </summary>
    public readonly struct DeckValidationResult
    {
        /// <summary>True when the deck satisfies every §5 rule.</summary>
        public readonly bool IsValid;

        /// <summary>The first rule that failed, or <see cref="DeckValidationError.None"/>.</summary>
        public readonly DeckValidationError Error;

        /// <summary>Calm, second-person, display-ready message. Empty when valid.</summary>
        public readonly string Message;

        /// <summary>Average elixir cost of the deck (0 when it could not be computed).</summary>
        public readonly double AverageElixir;

        /// <summary>Number of cards seen in the submitted deck.</summary>
        public readonly int CardCount;

        private DeckValidationResult(
            bool isValid,
            DeckValidationError error,
            string message,
            double averageElixir,
            int cardCount)
        {
            IsValid = isValid;
            Error = error;
            Message = message;
            AverageElixir = averageElixir;
            CardCount = cardCount;
        }

        /// <summary>Builds a success result carrying the computed average.</summary>
        public static DeckValidationResult Ok(double averageElixir) =>
            new DeckValidationResult(
                true,
                DeckValidationError.None,
                string.Empty,
                averageElixir,
                DeckValidator.RequiredCardCount);

        /// <summary>Builds a failure result for the first failed rule.</summary>
        public static DeckValidationResult Fail(
            DeckValidationError error,
            string message,
            double averageElixir,
            int cardCount) =>
            new DeckValidationResult(false, error, message, averageElixir, cardCount);
    }
}
