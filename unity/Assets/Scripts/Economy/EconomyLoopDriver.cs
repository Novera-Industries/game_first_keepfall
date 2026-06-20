using System.Text;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.Save;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Data;
using UnityEngine;

namespace Keepfall.Economy
{
    /// <summary>
    /// Minimal, editor-playable demonstration of the end-to-end economic loop (source-of-truth
    /// §13, milestone <c>milestone/01-economy</c>: "Editor-playable economic loop end-to-end").
    /// Drop this on a GameObject and press Play: it loads (or seeds) a save, refreshes tile
    /// accrual against the wall clock, lets you grant a tile from a "match win", claim Stone, and
    /// unlock a unit — printing the wallet at each step. It is a harness, not shipping UI; the
    /// real screens consume <see cref="TileService"/> and <see cref="EconomyLedger"/> directly.
    /// <para>
    /// This driver only orchestrates the systems this feature owns plus Core; it adds no economy
    /// rules of its own, so every number it shows still traces to remote config and §2.
    /// </para>
    /// </summary>
    public sealed class EconomyLoopDriver : MonoBehaviour
    {
        [Header("Demo save slot (kept separate from the real player save)")]
        [SerializeField] private string _saveFileName = "keepfall_economy_demo.json";

        [Header("Seed (only used the first time the demo save is created)")]
        [SerializeField] private long _seedStone = 100;
        [SerializeField] private TileRank _seedTileRank = TileRank.T1;

        [Header("Demo unit unlock")]
        [SerializeField] private string _demoUnitId = "core.longshot";
        [SerializeField] private UnlockTier _demoUnitTier = UnlockTier.Core;
        [SerializeField] private long _demoUnitCost = 300;

        private SaveSystem _save;
        private PlayerState _state;
        private Wallet _wallet;
        private RemoteConfig _config;
        private IAnalytics _analytics;
        private TileService _tiles;
        private EconomyLedger _ledger;

        private void Awake()
        {
            _config = RemoteConfigLoader.CreateWithBundledDefaults();
            _analytics = new DebugAnalytics();
            _save = new SaveSystem(new LocalSaveStore(_saveFileName));
            _state = _save.Load();

            // First run: seed a little Stone and one won tile so the loop has something to do.
            if (_state.Tiles.Count == 0 && _state.Wallet.Stone == 0)
            {
                _state.Wallet.Stone = _seedStone;
            }

            _wallet = new Wallet(_state.Wallet);
            _tiles = new TileService(
                _state.Tiles, _wallet, _config, _state.Subscription, _analytics);
            _ledger = new EconomyLedger(_wallet, _state.Roster, _analytics);

            if (_state.Tiles.Count == 0)
            {
                // A tile comes ONLY from a win — simulate one so the demo has yield to accrue.
                _tiles.GrantTileFromMatchWin(_seedTileRank, GameClock.UtcNow);
            }
        }

        private void Start()
        {
            // Accrual is computed from each tile's persisted anchor, so anything earned while the
            // editor (or app) was closed is credited here, on load.
            _tiles.RefreshAccrual(GameClock.UtcNow);
            LogState("Loaded and refreshed");
            Persist();
        }

        // ── Inspector context-menu actions (right-click the component) ────

        /// <summary>Recomputes accrual to "now" and prints every tile's held Stone.</summary>
        [ContextMenu("Refresh Accrual")]
        public void RefreshAccrualNow()
        {
            _tiles.RefreshAccrual(GameClock.UtcNow);
            LogState("Refreshed accrual");
            Persist();
        }

        /// <summary>Silently claims the first claimable tile and prints the calm result copy.</summary>
        [ContextMenu("Claim First Filled Tile")]
        public void ClaimFirstFilledTile()
        {
            _tiles.RefreshAccrual(GameClock.UtcNow);

            foreach (TileState tile in _state.Tiles)
            {
                if (_tiles.CanClaim(tile))
                {
                    ClaimResult result = _tiles.Claim(tile, GameClock.UtcNow);
                    Debug.Log($"[Economy] {result.Message}"); // silent claim: copy only, no modal
                    LogState("After claim");
                    Persist();
                    return;
                }
            }

            Debug.Log("[Economy] No tile has Stone to claim yet.");
        }

        /// <summary>Simulates a PvE win that grants a fresh tile (the only way a tile is made).</summary>
        [ContextMenu("Grant Tile From Match Win")]
        public void GrantTileFromWin()
        {
            TileState tile = _tiles.GrantTileFromMatchWin(_seedTileRank, GameClock.UtcNow);
            Debug.Log($"[Economy] Won Tile {tile.Id} (rank {tile.Rank}). Stone yield begins now.");
            LogState("After win");
            Persist();
        }

        /// <summary>Attempts the configured demo unit unlock (Stone only) and prints the result.</summary>
        [ContextMenu("Unlock Demo Unit (Stone)")]
        public void UnlockDemoUnit()
        {
            UnlockResult result = _ledger.UnlockUnit(_demoUnitId, _demoUnitTier, _demoUnitCost);
            Debug.Log($"[Economy] {result.Message}");
            LogState("After unlock attempt");
            Persist();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void Persist()
        {
            _save.Save(_state);
        }

        private void LogState(string label)
        {
            var sb = new StringBuilder();
            sb.Append("[Economy] ").Append(label).Append(" | Wallet: ")
              .Append(_wallet.GetBalance(CurrencyType.Stone)).Append(" Stone, ")
              .Append(_wallet.GetBalance(CurrencyType.Shards)).Append(" Shards");

            sb.Append(" | Tiles: ").Append(_state.Tiles.Count);
            foreach (TileState tile in _state.Tiles)
            {
                long cap = _tiles.EffectiveCap(tile);
                sb.Append(" [").Append(tile.Id).Append(' ').Append(tile.Rank)
                  .Append(": ").Append(tile.AccruedStone).Append('/').Append(cap).Append(']');
            }

            sb.Append(" | Units: ").Append(_state.Roster.UnlockedUnitIds.Count);
            Debug.Log(sb.ToString());
        }
    }
}
