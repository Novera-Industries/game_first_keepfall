using System;
using System.Collections.Generic;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;

namespace Keepfall.Economy
{
    /// <summary>
    /// Owns the live tile economy (source-of-truth §2): refreshing accrual against the wall
    /// clock, claiming a filled tile's Stone into the wallet, and creating tiles from match
    /// wins. Operates on a caller-supplied <see cref="TileState"/> list (the same list held by
    /// <see cref="PlayerState.Tiles"/>) plus the <see cref="Wallet"/>, so the save round-trip
    /// and this service share one source of truth.
    /// <para>
    /// Invariants enforced here:
    /// <list type="bullet">
    ///   <item><b>Tiles come only from winning combat.</b> <see cref="GrantTileFromMatchWin"/>
    ///   is the ONLY method that adds a tile. No spend path exists, by design (§2, §10.2).</item>
    ///   <item><b>Claiming is silent.</b> No modal/confetti hook is fired; the only feedback is
    ///   a calm result string and the wallet balance change (§2, §12).</item>
    ///   <item><b>Yield survives app close.</b> Accrual is recomputed from each tile's persisted
    ///   <see cref="TileState.LastAccrualUtc"/> via <see cref="TileYield"/> (§2).</item>
    /// </list>
    /// </para>
    /// Pure C# (no UnityEngine) so the whole loop is EditMode-testable.
    /// </summary>
    public sealed class TileService
    {
        private readonly List<TileState> _tiles;
        private readonly Wallet _wallet;
        private readonly RemoteConfig _config;
        private readonly SubscriptionState _subscription;
        private readonly IAnalytics _analytics;

        /// <summary>
        /// Wires the service to the live save objects. All references are required except
        /// <paramref name="analytics"/>, which may be null in headless tests.
        /// </summary>
        /// <param name="tiles">The owned-tile list (typically <see cref="PlayerState.Tiles"/>).</param>
        /// <param name="wallet">The wallet claims credit Stone into.</param>
        /// <param name="config">Remote config supplying rate, cap, and the Plus multiplier.</param>
        /// <param name="subscription">Plus state; drives the +50% yield perk.</param>
        /// <param name="analytics">Optional analytics sink for <c>tile.*</c> events.</param>
        public TileService(
            List<TileState> tiles,
            Wallet wallet,
            RemoteConfig config,
            SubscriptionState subscription,
            IAnalytics analytics = null)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
            _analytics = analytics;
        }

        /// <summary>The owned tiles (read-only view for UI/inspection).</summary>
        public IReadOnlyList<TileState> Tiles => _tiles;

        /// <summary>The effective pre-claim cap for <paramref name="tile"/> right now (Plus-aware).</summary>
        public long EffectiveCap(TileState tile)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            return TileYield.Cap(_config, tile.Rank, _subscription.Active);
        }

        /// <summary>
        /// Recomputes accrual for every owned tile up to <paramref name="now"/> and advances each
        /// tile's <see cref="TileState.LastAccrualUtc"/> anchor to <paramref name="now"/>. Safe to
        /// call on launch, on resume, and before any claim. Idempotent for a fixed
        /// <paramref name="now"/>. Emits <see cref="Events.TileCapReached"/> once per tile when a
        /// tile newly reaches its cap.
        /// </summary>
        public void RefreshAccrual(DateTimeOffset now)
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                RefreshTile(_tiles[i], now);
            }
        }

        /// <summary>Recomputes a single tile's accrual up to <paramref name="now"/>.</summary>
        public void RefreshTile(TileState tile, DateTimeOffset now)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            long cap = TileYield.Cap(_config, tile.Rank, _subscription.Active);
            bool wasBelowCap = tile.AccruedStone < cap;

            long updated = TileYield.AccrueTo(_config, tile, _subscription.Active, now);
            tile.AccruedStone = updated;

            // Advance the anchor so the next delta is measured from here. Never move it backwards
            // (a backwards clock must not let a future delta double-count).
            if (now > tile.LastAccrualUtc)
            {
                tile.LastAccrualUtc = now;
            }

            if (wasBelowCap && updated >= cap)
            {
                Track(Events.TileCapReached, tile, cap);
            }
        }

        /// <summary>
        /// True when <paramref name="tile"/> holds at least 1 Stone to claim. Refresh accrual
        /// first if you need an up-to-the-second answer.
        /// </summary>
        public bool CanClaim(TileState tile)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            return tile.AccruedStone > 0;
        }

        /// <summary>
        /// SILENT claim (source-of-truth §2 + §12): moves the tile's accrued Stone into the
        /// wallet, resets accrual, advances both timestamps to <paramref name="now"/>, emits
        /// <see cref="Events.TileClaimed"/>, and returns a calm second-person result string.
        /// No modal, confetti, or celebratory hook is fired — the wallet change and the returned
        /// copy are the only feedback. Returns a no-op result (claimed = 0) when nothing is owed,
        /// so callers can call it unconditionally.
        /// </summary>
        public ClaimResult Claim(TileState tile, DateTimeOffset now)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            // Bring the tile current before reading the balance, so a tap claims everything
            // earned up to this instant (covers the "filled while the app was closed" case).
            RefreshTile(tile, now);

            long amount = tile.AccruedStone;
            if (amount <= 0)
            {
                return ClaimResult.Empty(tile.Id);
            }

            _wallet.Add(CurrencyType.Stone, amount);

            tile.AccruedStone = 0;
            tile.LastClaimUtc = now;
            tile.LastAccrualUtc = now;

            Track(Events.TileClaimed, tile, amount);

            // Calm, honest, second-person. No exclamation points (§12).
            string message = $"You claimed Tile {tile.Id}. Stone yield begins now.";
            return new ClaimResult(true, tile.Id, amount, message);
        }

        /// <summary>
        /// Creates a tile of <paramref name="rank"/> anchored at <paramref name="wonAtUtc"/> and
        /// appends it to the owned list. This is the ONLY way a tile is created in Keepfall:
        /// tiles are won in PvE combat, never granted by any spend (§2, §10.2). Emits
        /// <see cref="Events.TileAcquired"/>.
        /// </summary>
        /// <param name="rank">Rank of the won tile.</param>
        /// <param name="wonAtUtc">UTC the match was won; the accrual anchor.</param>
        /// <param name="id">Optional explicit id; auto-assigned (zero-padded) when null/empty.</param>
        public TileState GrantTileFromMatchWin(
            TileRank rank,
            DateTimeOffset wonAtUtc,
            string id = null)
        {
            string tileId = string.IsNullOrEmpty(id) ? NextTileId() : id;
            var tile = new TileState(tileId, rank, wonAtUtc);
            _tiles.Add(tile);

            Track(Events.TileAcquired, tile, 0);
            return tile;
        }

        // ── Internals ────────────────────────────────────────────────────

        /// <summary>
        /// Generates the next zero-padded tile id. Ids are sequential strings ("01", "02", …)
        /// matching the claim-copy format (`"You claimed Tile 07. …"`). Reuses the lowest free
        /// number so ids stay stable and human-readable across a session.
        /// </summary>
        private string NextTileId()
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (TileState t in _tiles)
            {
                if (!string.IsNullOrEmpty(t.Id))
                {
                    used.Add(t.Id);
                }
            }

            int n = 1;
            string candidate;
            do
            {
                candidate = n.ToString("00");
                n++;
            }
            while (used.Contains(candidate));

            return candidate;
        }

        private void Track(string evt, TileState tile, long amount)
        {
            if (_analytics == null)
            {
                return;
            }

            _analytics.Track(evt, new Dictionary<string, object>
            {
                ["tile_id"] = tile.Id,
                ["rank"] = tile.Rank.ToString(),
                ["amount"] = amount,
                ["plus_active"] = _subscription.Active,
            });
        }
    }

    /// <summary>
    /// Outcome of a <see cref="TileService.Claim"/>. Deliberately data-only and silent: the UI
    /// reads <see cref="Message"/> and updates the wallet counter — there is no celebratory
    /// payload because claiming is a quiet, honest moment (source-of-truth §2, §12).
    /// </summary>
    public readonly struct ClaimResult
    {
        /// <summary>True when Stone was actually moved into the wallet.</summary>
        public readonly bool Claimed;

        /// <summary>Id of the tile the claim targeted.</summary>
        public readonly string TileId;

        /// <summary>Stone moved into the wallet (0 when nothing was owed).</summary>
        public readonly long StoneClaimed;

        /// <summary>Calm second-person copy to surface to the player.</summary>
        public readonly string Message;

        /// <summary>Creates a claim result.</summary>
        public ClaimResult(bool claimed, string tileId, long stoneClaimed, string message)
        {
            Claimed = claimed;
            TileId = tileId;
            StoneClaimed = stoneClaimed;
            Message = message;
        }

        /// <summary>A no-op result for a tile that had nothing to claim.</summary>
        public static ClaimResult Empty(string tileId) =>
            new ClaimResult(false, tileId, 0, $"Tile {tileId} has no Stone to claim yet.");
    }
}
