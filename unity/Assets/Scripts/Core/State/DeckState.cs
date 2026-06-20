using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// A single saved deck: exactly 8 unit ids (source-of-truth §4–§5). The 8-card-count and
    /// composition rules (avg elixir 2.6–3.0, ≥1 Vanguard, ≥1 Champion) are enforced by the
    /// Deck-feature validator that operates on this DTO — not here.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Deck
    {
        /// <summary>The 8 unit ids in this deck. Size validation is the validator's job; the
        /// holder does not enforce count so partially-edited decks can be saved.</summary>
        [JsonProperty("unitIds")]
        public List<string> UnitIds = new List<string>();

        /// <summary>Required by the serializer.</summary>
        public Deck()
        {
        }
    }

    /// <summary>
    /// Save state for the player's decks and slot ownership (source-of-truth §5).
    /// Slot ladder: 3 (F2P) · 4 (Plus) · up to 6 (purchased expansion). The Plus slot is a
    /// convenience perk, not a power perk — it expands loadout flexibility only.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class DeckState
    {
        /// <summary>Default unlocked slot count for a fresh F2P player (§5).</summary>
        public const int DefaultSlotsUnlocked = 3;

        /// <summary>All saved decks. Length tracks <see cref="SlotsUnlocked"/>.</summary>
        [JsonProperty("decks")]
        public List<Deck> Decks = new List<Deck>();

        /// <summary>Index into <see cref="Decks"/> for the active loadout.</summary>
        [JsonProperty("selectedDeckIndex")]
        public int SelectedDeckIndex;

        /// <summary>How many deck slots the player currently has (3 F2P, 4 Plus, up to 6).</summary>
        [JsonProperty("slotsUnlocked")]
        public int SlotsUnlocked = DefaultSlotsUnlocked;

        /// <summary>Required by the serializer.</summary>
        public DeckState()
        {
        }
    }
}
