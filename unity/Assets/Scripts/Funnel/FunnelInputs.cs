using System;
using System.Linq;
using Keepfall.Core.State;

namespace Keepfall.Funnel
{
    /// <summary>
    /// Immutable player-STATE snapshot the <see cref="FunnelEngine"/> reads to decide which
    /// trigger, if any, to surface. PRECONDITION is always player STATE, never wall-clock alone
    /// (source-of-truth §8): <see cref="DayIndex"/> may <i>gate eligibility</i>, but a state
    /// field must also be satisfied for any trigger to fire.
    ///
    /// <para>
    /// This is built from the canonical <see cref="PlayerState"/> save plus a handful of live
    /// gameplay signals that Core's plain-data holders do not themselves track (e.g. "lost an
    /// Adept match", "unlock pacing has slowed"). The composition root assembles it once per
    /// evaluation; the engine stays pure and EditMode-testable. The <see cref="FunnelState"/>
    /// reference is the engine's bookkeeping target for frequency caps.
    /// </para>
    /// </summary>
    public readonly struct FunnelInputs
    {
        // ── Time / cohort ────────────────────────────────────────────────
        /// <summary>Days since install (0 on install day). The expected correlation, NOT the
        /// gate — a state field must also be satisfied (SoT §8).</summary>
        public readonly int DayIndex;

        /// <summary>Now (UTC), read from the injected clock so caps are deterministic in tests.</summary>
        public readonly DateTimeOffset NowUtc;

        // ── Economy / progression state ──────────────────────────────────
        /// <summary>Number of tiles the player currently owns (SoT §2).</summary>
        public readonly int TilesOwned;

        /// <summary>Number of units the player currently owns (roster size, SoT §3).</summary>
        public readonly int RosterSize;

        /// <summary>The base starter-roster size (6, SoT §3). A roster larger than this means at
        /// least one unit was unlocked OUTSIDE the starters.</summary>
        public readonly int StarterRosterSize;

        /// <summary>Current Stone balance.</summary>
        public readonly long StoneBalance;

        /// <summary>True when the player has hit a Stone wall: they want a unit they cannot yet
        /// afford. Derived by the economy layer (next-desired-unlock cost &gt; balance, SoT §8 D3).</summary>
        public readonly bool HasHitStoneWall;

        /// <summary>True when the player owns at least one T3 tile (SoT §8 D11 precondition).</summary>
        public readonly bool OwnsT3Tile;

        /// <summary>True when the player faces a specialist Stone wall (desired specialist unlock
        /// 2,500–6,000 Stone unaffordable, SoT §8 D11).</summary>
        public readonly bool FacesSpecialistWall;

        /// <summary>Tile id the accelerator hint would attach to (the most-filled eligible T3
        /// tile). Used for the per-tile hint cap. Null when none qualifies.</summary>
        public readonly string CandidateAcceleratorTileId;

        /// <summary>True once a tile has reached its cap or been viewed while accruing — i.e. the
        /// player has WAITED on tile yield at least once (SoT §8 D2 precondition).</summary>
        public readonly bool HasWaitedOnTileYield;

        // ── Combat / retry state ─────────────────────────────────────────
        /// <summary>True once the player has lost at least one Adept (AI tier 2) match
        /// (SoT §8 D4 precondition).</summary>
        public readonly bool HasLostAdeptMatch;

        /// <summary>Locally-known retry-token balance (server is authoritative, SoT §6 P3).</summary>
        public readonly int RetryTokenCount;

        /// <summary>The match seed currently on the loss screen (the seed the player just lost on),
        /// or null when not on a loss screen.</summary>
        public readonly string CurrentLossMatchSeed;

        /// <summary>Consecutive losses on <see cref="CurrentLossMatchSeed"/> (SoT §8 retry rule).
        /// 0 when not applicable.</summary>
        public readonly int CurrentMatchLossStreak;

        // ── Engagement / pacing ──────────────────────────────────────────
        /// <summary>True when the player is engaged and exploring synergies: enough matches across
        /// at least two distinct decks (SoT §8 D8 precondition).</summary>
        public readonly bool IsExploringSynergies;

        /// <summary>True when unit-unlock pacing has slowed past the pacing threshold (time since
        /// last unlock exceeds it, SoT §8 D7 precondition).</summary>
        public readonly bool UnlockPacingSlowed;

        // ── Conversion / subscription state ──────────────────────────────
        /// <summary>True when Keepfall Plus is currently active (SoT §6 P2).</summary>
        public readonly bool IsPlusActive;

        /// <summary>True when the player is a CONVERTER: Plus active, or has ever spent real money
        /// in a meaningful way. Drives the post-D30 hard branch and the "already converted"
        /// suppression (SoT §8.2).</summary>
        public readonly bool IsConverter;

        // ── Bookkeeping target ───────────────────────────────────────────
        /// <summary>The funnel save state the engine reads + updates for frequency caps. Same
        /// reference held by <see cref="PlayerState.Funnel"/>.</summary>
        public readonly FunnelState Funnel;

        /// <summary>Full constructor (used by the composition root and tests that want explicit
        /// control over every signal).</summary>
        public FunnelInputs(
            int dayIndex,
            DateTimeOffset nowUtc,
            int tilesOwned,
            int rosterSize,
            int starterRosterSize,
            long stoneBalance,
            bool hasHitStoneWall,
            bool ownsT3Tile,
            bool facesSpecialistWall,
            string candidateAcceleratorTileId,
            bool hasWaitedOnTileYield,
            bool hasLostAdeptMatch,
            int retryTokenCount,
            string currentLossMatchSeed,
            int currentMatchLossStreak,
            bool isExploringSynergies,
            bool unlockPacingSlowed,
            bool isPlusActive,
            bool isConverter,
            FunnelState funnel)
        {
            DayIndex = dayIndex;
            NowUtc = nowUtc;
            TilesOwned = tilesOwned;
            RosterSize = rosterSize;
            StarterRosterSize = starterRosterSize;
            StoneBalance = stoneBalance;
            HasHitStoneWall = hasHitStoneWall;
            OwnsT3Tile = ownsT3Tile;
            FacesSpecialistWall = facesSpecialistWall;
            CandidateAcceleratorTileId = candidateAcceleratorTileId;
            HasWaitedOnTileYield = hasWaitedOnTileYield;
            HasLostAdeptMatch = hasLostAdeptMatch;
            RetryTokenCount = retryTokenCount;
            CurrentLossMatchSeed = currentLossMatchSeed;
            CurrentMatchLossStreak = currentMatchLossStreak;
            IsExploringSynergies = isExploringSynergies;
            UnlockPacingSlowed = unlockPacingSlowed;
            IsPlusActive = isPlusActive;
            IsConverter = isConverter;
            Funnel = funnel ?? throw new ArgumentNullException(nameof(funnel));
        }

        /// <summary>
        /// True if the player has unlocked at least one unit OUTSIDE the 6 starters — the D3
        /// precondition (SoT §8 D3). Derived purely from roster size vs the starter count, so it
        /// never requires hardcoded unit ids.
        /// </summary>
        public bool HasUnlockedOutsideStarters => RosterSize > StarterRosterSize;

        /// <summary>
        /// Seeds the economy/progression fields straight off a <see cref="PlayerState"/> so the
        /// composition root only has to supply the live gameplay/derived signals. This is the
        /// "reads PlayerState" path: tiles, roster, Stone, subscription, and the funnel
        /// bookkeeping all come from the canonical save.
        /// </summary>
        public static FunnelInputs FromPlayerState(
            PlayerState state,
            DateTimeOffset nowUtc,
            int starterRosterSize = 6,
            bool hasHitStoneWall = false,
            bool ownsT3Tile = false,
            bool facesSpecialistWall = false,
            string candidateAcceleratorTileId = null,
            bool hasWaitedOnTileYield = false,
            bool hasLostAdeptMatch = false,
            string currentLossMatchSeed = null,
            bool isExploringSynergies = false,
            bool unlockPacingSlowed = false,
            bool isConverter = false)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            bool plusActive = state.Subscription != null && state.Subscription.Active;
            int lossStreak = 0;
            if (currentLossMatchSeed != null
                && state.Retry != null
                && state.Retry.PerMatchLossStreak != null
                && state.Retry.PerMatchLossStreak.TryGetValue(currentLossMatchSeed, out int s))
            {
                lossStreak = s;
            }

            // OwnsT3Tile can also be derived from the save when the caller does not assert it.
            bool ownsT3 = ownsT3Tile
                || (state.Tiles != null && state.Tiles.Any(t => t != null && t.Rank == TileRank.T3));

            return new FunnelInputs(
                dayIndex: state.Funnel?.DayIndex ?? 0,
                nowUtc: nowUtc,
                tilesOwned: state.Tiles?.Count ?? 0,
                rosterSize: state.Roster?.UnlockedUnitIds?.Count ?? 0,
                starterRosterSize: starterRosterSize,
                stoneBalance: state.Wallet?.Stone ?? 0,
                hasHitStoneWall: hasHitStoneWall,
                ownsT3Tile: ownsT3,
                facesSpecialistWall: facesSpecialistWall,
                candidateAcceleratorTileId: candidateAcceleratorTileId,
                hasWaitedOnTileYield: hasWaitedOnTileYield,
                hasLostAdeptMatch: hasLostAdeptMatch,
                retryTokenCount: state.Retry?.TokenCount ?? 0,
                currentLossMatchSeed: currentLossMatchSeed,
                currentMatchLossStreak: lossStreak,
                isExploringSynergies: isExploringSynergies,
                unlockPacingSlowed: unlockPacingSlowed,
                isPlusActive: plusActive,
                // A Plus subscriber is by definition a converter (SoT §8.2 "no Plus, no
                // meaningful spend" defines a NON-converter).
                isConverter: isConverter || plusActive,
                funnel: state.Funnel ?? new FunnelState());
        }
    }
}
