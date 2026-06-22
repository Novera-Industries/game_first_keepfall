using System;
using System.Collections.Generic;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Economy;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Milestone 03 ("tile interaction loop complete", source-of-truth §13) integration test: it
    /// drives the WHOLE loop through the real services — a tile won in combat (§2) accrues over
    /// time, is accelerated to cap for Shards (§6 Product 1), then claimed silently into the wallet
    /// (§2). The per-rule accelerator caps are locked by <c>AcceleratorCapTests</c>; this proves the
    /// services compose end to end. Deterministic via <see cref="FakeTimeProvider"/>.
    /// </summary>
    public sealed class TileInteractionLoopTests
    {
        private static readonly DateTimeOffset D1Start =
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void FullLoop_Accrue_Accelerate_Claim_BanksOneCapAndChargesRankPrice()
        {
            var clock = new FakeTimeProvider(D1Start);
            var config = new RemoteConfig();              // canonical defaults (T1: 10/hr, cap 120, price 15)
            var wallet = new Wallet(new WalletState(0, 60)); // 0 Stone, 60 Shards seeded
            var subscription = new SubscriptionState();   // no Plus
            var tiles = new List<TileState>();
            var tileService = new TileService(tiles, wallet, config, subscription, analytics: null);

            // A tile comes ONLY from a win (§2) — never from spend.
            TileState tile = tileService.GrantTileFromMatchWin(TileRank.T1, clock.UtcNow);
            Assert.AreEqual(1, tiles.Count, "A win must create exactly one tile.");
            long cap = tileService.EffectiveCap(tile);
            Assert.AreEqual(120, cap, "T1 effective cap is 120 at canonical defaults.");

            // 1) ACCRUE — five hours of real-time yield (10/hr → ~50 of 120, past the 30% floor).
            clock.AdvanceHours(5);
            tileService.RefreshAccrual(clock.UtcNow);
            Assert.That(tile.AccruedStone, Is.InRange(cap * 3 / 10, cap - 1),
                "After 5h the tile is between the 30% offer floor and the cap.");
            long accruedBeforeAccelerate = tile.AccruedStone;

            // 2) ACCELERATE — fill to cap for the rank Shard price (re-checks every §6 cap).
            var accelerator = new YieldAccelerator(wallet, config, analytics: null, time: clock);
            AccelerateOffer offer = accelerator.CanOffer(tile, D1Start);
            Assert.IsTrue(offer.CanOffer, offer.Message);
            Assert.AreEqual(15, offer.PriceShards, "T1 accelerator price is 15 Shards.");
            Assert.AreEqual(cap - accruedBeforeAccelerate, offer.StoneToAdd,
                "A fill-to-cap offer adds exactly the gap to the cap — never beyond one cap (§6).");

            AccelerateResult result = accelerator.ApplyAccelerate(tile, D1Start);
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(15, result.ShardsSpent);
            Assert.AreEqual(45, wallet.GetBalance(CurrencyType.Shards), "60 − 15 Shards.");
            Assert.AreEqual(cap, tile.AccruedStone, "Accelerate fills to the cap exactly.");

            // 3) CLAIM — silent; the full cap of Stone enters the wallet, tile resets.
            ClaimResult claim = tileService.Claim(tile, clock.UtcNow);
            Assert.IsTrue(claim.Claimed);
            Assert.AreEqual(cap, claim.StoneClaimed);
            Assert.AreEqual(cap, wallet.GetBalance(CurrencyType.Stone), "Claimed Stone reaches the wallet.");
            Assert.AreEqual(0, tile.AccruedStone, "A claimed tile resets to zero and begins accruing again.");
            // Calm copy, no exclamation point (§12).
            Assert.IsFalse(claim.Message.Contains("!"), "UI copy carries no exclamation points (§12).");
        }

        [Test]
        public void Accelerate_IsRefused_WhenWalletCannotAffordTheRankPrice()
        {
            var clock = new FakeTimeProvider(D1Start);
            var config = new RemoteConfig();
            var wallet = new Wallet(new WalletState(0, 5)); // only 5 Shards; T1 costs 15
            var subscription = new SubscriptionState();
            var tiles = new List<TileState>();
            var tileService = new TileService(tiles, wallet, config, subscription, analytics: null);

            TileState tile = tileService.GrantTileFromMatchWin(TileRank.T1, clock.UtcNow);
            clock.AdvanceHours(5);
            tileService.RefreshAccrual(clock.UtcNow);

            var accelerator = new YieldAccelerator(wallet, config, analytics: null, time: clock);
            AccelerateResult result = accelerator.ApplyAccelerate(tile, D1Start);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AccelerateRefusal.InsufficientShards, result.Refusal);
            Assert.AreEqual(5, wallet.GetBalance(CurrencyType.Shards), "A refused accelerate charges nothing.");
            Assert.Less(tile.AccruedStone, tileService.EffectiveCap(tile), "The tile was not filled.");
        }
    }
}
