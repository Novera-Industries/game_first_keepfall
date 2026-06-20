using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Save state for the 30-day conversion funnel (source-of-truth §8). The trigger engine
    /// (Funnel assembly) reads PLAYER STATE — never wall-clock alone — and uses these fields
    /// to honour frequency caps:
    /// <list type="bullet">
    ///   <item>Keepfall Plus reveals: max 3 / 30 days (see <see cref="PlusRevealCount"/> and
    ///   key <c>funnel.plus.maxReveals</c>); after D30 a non-converter sees NO new triggers.</item>
    ///   <item>Accelerator hints: max 1 per tile per week (see
    ///   <see cref="PerTileAcceleratorHintUtc"/>).</item>
    ///   <item>Dismissed triggers are remembered so they are not re-shown inappropriately.</item>
    /// </list>
    /// This is the data holder; the cap/branch logic lives in the engine.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FunnelState
    {
        /// <summary>UTC of first install. Anchor for <see cref="DayIndex"/> and the D30 cliff.</summary>
        [JsonProperty("installUtc")]
        public DateTimeOffset InstallUtc;

        /// <summary>UTC of the most recent session start.</summary>
        [JsonProperty("lastSeenUtc")]
        public DateTimeOffset LastSeenUtc;

        /// <summary>Days since install (0-based on D1). Convenience cache; the engine still
        /// gates on player state, not this number alone.</summary>
        [JsonProperty("dayIndex")]
        public int DayIndex;

        /// <summary>Last-shown UTC per funnel trigger id (e.g. "plus.reveal", "starter.pack").
        /// Drives per-trigger frequency caps.</summary>
        [JsonProperty("perTriggerLastShownUtc")]
        public Dictionary<string, DateTimeOffset> PerTriggerLastShownUtc =
            new Dictionary<string, DateTimeOffset>();

        /// <summary>How many times the Keepfall Plus reveal has been shown. Hard cap 3 / 30
        /// days (§8), enforced by the engine against <c>funnel.plus.maxReveals</c>.</summary>
        [JsonProperty("plusRevealCount")]
        public int PlusRevealCount;

        /// <summary>Trigger ids the player explicitly dismissed; the engine respects these.</summary>
        [JsonProperty("dismissedTriggers")]
        public List<string> DismissedTriggers = new List<string>();

        /// <summary>Last accelerator-hint UTC per tile id. Enforces "max 1 per tile per week"
        /// and "never to a player who used an accelerator in the past 7 days" (§8).</summary>
        [JsonProperty("perTileAcceleratorHintUtc")]
        public Dictionary<string, DateTimeOffset> PerTileAcceleratorHintUtc =
            new Dictionary<string, DateTimeOffset>();

        /// <summary>Required by the serializer.</summary>
        public FunnelState()
        {
        }
    }
}
