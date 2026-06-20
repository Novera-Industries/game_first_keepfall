using System;
using Newtonsoft.Json;

namespace Keepfall.Core.State
{
    /// <summary>
    /// Save state for a single owned tile (source-of-truth §2). A tile is earned ONLY by
    /// winning a PvE match — never by spend — so this DTO is created by combat resolution,
    /// never by the shop. Accrual math (rate * elapsed, clamped to cap) is owned by the
    /// Economy feature assembly and operates on these fields; this class is just the holder.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TileState
    {
        /// <summary>Stable tile id (e.g. "07"), used in claim copy and analytics.</summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>Rank tier driving yield rate and cap.</summary>
        [JsonProperty("rank")]
        public TileRank Rank = TileRank.T1;

        /// <summary>Stone accrued and waiting to be claimed. Clamped to the rank cap by the
        /// accrual logic; persisted so yield survives app close.</summary>
        [JsonProperty("accruedStone")]
        public long AccruedStone;

        /// <summary>UTC of the last successful claim (Stone moved to wallet). Used for
        /// claim-flow copy and analytics.</summary>
        [JsonProperty("lastClaimUtc")]
        public DateTimeOffset LastClaimUtc;

        /// <summary>UTC anchor the next accrual delta is measured from. Advanced on every
        /// accrual recompute and on claim. Wall-clock delta against this on resume is what
        /// lets yield survive app restart.</summary>
        [JsonProperty("lastAccrualUtc")]
        public DateTimeOffset LastAccrualUtc;

        /// <summary>Required by the serializer.</summary>
        public TileState()
        {
        }

        /// <summary>Creates a freshly-won tile whose accrual anchors at <paramref name="wonAtUtc"/>.</summary>
        public TileState(string id, TileRank rank, DateTimeOffset wonAtUtc)
        {
            Id = id;
            Rank = rank;
            AccruedStone = 0;
            LastClaimUtc = wonAtUtc;
            LastAccrualUtc = wonAtUtc;
        }
    }
}
