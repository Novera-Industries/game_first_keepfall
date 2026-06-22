using System;
using System.Collections.Generic;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Economy;
using Keepfall.Monetization;
using UnityEditor;
using UnityEngine;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// Milestone 03 on-ramp (<c>milestone/03-accelerator</c>: "tile interaction loop complete",
    /// source-of-truth §13). Runs the COMPLETE tile interaction loop headlessly and prints each
    /// step to the Console: a tile won in combat (§2) accrues over time, is accelerated to cap for
    /// Shards (§6 Product 1, with every hard cap re-checked), then claimed silently into the wallet
    /// (§2). Deterministic via a <see cref="FakeTimeProvider"/> — no Play mode required.
    /// <para>Menu: <b>Keepfall ▸ Tile ▸ Run Tile Interaction Loop</b>. The loop logic lives in
    /// <see cref="TileService"/> + <see cref="YieldAccelerator"/>; this only orchestrates them.</para>
    /// </summary>
    public static class TileInteractionDemo
    {
        [MenuItem("Keepfall/Tile/Run Tile Interaction Loop")]
        public static void RunLoop()
        {
            var config = new RemoteConfig();              // canonical defaults (T1: 10/hr, cap 120, price 15)
            var analytics = new DebugAnalytics();

            // Shards seeded for the demo. In the real game they come from a validated IAP pack
            // (milestone 02) — never granted free; seeded here only so the accelerate step can pay.
            var wallet = new Wallet(new WalletState(0, 60));

            var start = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
            var clock = new FakeTimeProvider(start);
            DateTimeOffset firstD1Play = start; // 5h of accrual below clears the 15-min D1 lock

            var tiles = new List<TileState>();
            var subscription = new SubscriptionState();
            var tileService = new TileService(tiles, wallet, config, subscription, analytics);

            // A tile comes ONLY from a win (§2).
            TileState tile = tileService.GrantTileFromMatchWin(TileRank.T1, clock.UtcNow);
            Debug.Log($"[Tile] Won {tile.Id} (T1). Stone yield begins now. {Describe(wallet)}");

            // 1) ACCRUE — five hours pass.
            clock.AdvanceHours(5);
            tileService.RefreshAccrual(clock.UtcNow);
            Debug.Log($"[Tile] After 5h: {tile.AccruedStone}/{tileService.EffectiveCap(tile)} Stone accrued.");

            // 2) ACCELERATE — fill to cap for Shards (re-checks every §6 hard cap).
            var accelerator = new YieldAccelerator(wallet, config, analytics, clock);
            AccelerateOffer offer = accelerator.CanOffer(tile, firstD1Play);
            Debug.Log($"[Tile] Offer: canOffer={offer.CanOffer}, price={offer.PriceShards} Shards, " +
                      $"wouldAdd={offer.StoneToAdd}. \"{offer.Message}\"");

            if (offer.CanOffer)
            {
                AccelerateResult acc = accelerator.ApplyAccelerate(tile, firstD1Play);
                Debug.Log($"[Tile] Accelerated: +{acc.StoneAdded} Stone, -{acc.ShardsSpent} Shards. " +
                          $"Tile now {tile.AccruedStone}/{tileService.EffectiveCap(tile)}. {Describe(wallet)}");
            }

            // 3) CLAIM — silent; Stone enters the wallet (§2, §12: no confetti, no modal).
            ClaimResult claim = tileService.Claim(tile, clock.UtcNow);
            Debug.Log($"[Tile] {claim.Message} {Describe(wallet)}");

            Debug.Log("[Tile] Tile interaction loop complete: accrue -> accelerate -> claim.");
        }

        private static string Describe(Wallet w) =>
            $"Wallet: {w.GetBalance(CurrencyType.Stone)} Stone, {w.GetBalance(CurrencyType.Shards)} Shards.";
    }
}
