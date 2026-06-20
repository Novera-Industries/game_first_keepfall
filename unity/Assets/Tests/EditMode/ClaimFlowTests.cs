using System;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Economy;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Claim-flow checks for <see cref="TileService.Claim"/> (source-of-truth §2 claim flow,
    /// §12 tone): claiming moves Stone into the wallet, resets accrual, is SILENT (no
    /// modal/confetti/celebration hook — only the calm copy and the wallet change), and a tile
    /// with nothing owed cannot be claimed.
    /// </summary>
    public sealed class ClaimFlowTests
    {
        private static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

        private RemoteConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new RemoteConfig(); // canonical fallbacks
        }

        private (TileService service, PlayerState state, Wallet wallet, RecordingAnalytics fx)
            NewLoop()
        {
            var state = new PlayerState();
            var wallet = new Wallet(state.Wallet);
            var fx = new RecordingAnalytics();
            var service = new TileService(state.Tiles, wallet, _config, state.Subscription, fx);
            return (service, state, wallet, fx);
        }

        [Test]
        public void Claim_MovesAccruedStoneIntoWallet_AndResetsAccrual()
        {
            (TileService service, PlayerState state, Wallet wallet, _) = NewLoop();
            TileState tile = service.GrantTileFromMatchWin(TileRank.T1, T0, id: "07");

            // 5 hours -> 50 Stone on a T1 tile.
            DateTimeOffset now = T0.AddHours(5);
            service.RefreshAccrual(now);
            Assert.AreEqual(50, tile.AccruedStone);

            ClaimResult result = service.Claim(tile, now);

            Assert.IsTrue(result.Claimed);
            Assert.AreEqual(50, result.StoneClaimed);
            Assert.AreEqual(50, wallet.GetBalance(CurrencyType.Stone), "Stone moved into wallet.");
            Assert.AreEqual(0, tile.AccruedStone, "Accrual resets to 0 after claim.");
            Assert.AreEqual(now, tile.LastClaimUtc);
            Assert.AreEqual(now, tile.LastAccrualUtc, "Anchor advances to the claim instant.");
        }

        [Test]
        public void Claim_ReturnsCalmSecondPersonCopy_NoExclamation()
        {
            (TileService service, _, _, _) = NewLoop();
            TileState tile = service.GrantTileFromMatchWin(TileRank.T1, T0, id: "07");
            service.RefreshAccrual(T0.AddHours(3));

            ClaimResult result = service.Claim(tile, T0.AddHours(3));

            Assert.AreEqual("You claimed Tile 07. Stone yield begins now.", result.Message);
            StringAssert.DoesNotContain("!", result.Message, "UI copy must never shout (§12).");
        }

        [Test]
        public void Claim_IsSilent_EmitsOnlyTileClaimed_NoCelebrationOrModalEvent()
        {
            (TileService service, _, _, RecordingAnalytics fx) = NewLoop();
            TileState tile = service.GrantTileFromMatchWin(TileRank.T1, T0, id: "07");
            service.RefreshAccrual(T0.AddHours(6));

            // Reset the spy so we only observe events caused by the claim itself.
            fx.Events.Clear();
            service.Claim(tile, T0.AddHours(6));

            // Exactly one event — the analytics record — and it is the claim event.
            Assert.AreEqual(1, fx.Events.Count, "A silent claim fires no extra hooks.");
            Assert.AreEqual(Events.TileClaimed, fx.Events[0].Event);

            // Defensive: no celebratory / modal-style event of any kind was emitted.
            foreach (RecordingAnalytics.Entry e in fx.Events)
            {
                StringAssert.DoesNotContain("modal", e.Event);
                StringAssert.DoesNotContain("confetti", e.Event);
                StringAssert.DoesNotContain("celebrat", e.Event);
            }
        }

        [Test]
        public void Claim_EmptyTile_IsRefused_NothingMoves()
        {
            (TileService service, _, Wallet wallet, RecordingAnalytics fx) = NewLoop();
            TileState tile = service.GrantTileFromMatchWin(TileRank.T1, T0, id: "07");

            // No time has passed; nothing accrued.
            fx.Events.Clear();
            ClaimResult result = service.Claim(tile, T0);

            Assert.IsFalse(result.Claimed, "An empty tile cannot be claimed.");
            Assert.AreEqual(0, result.StoneClaimed);
            Assert.AreEqual(0, wallet.GetBalance(CurrencyType.Stone));
            Assert.AreEqual(0, fx.CountOf(Events.TileClaimed), "No claim event for an empty tile.");
        }

        [Test]
        public void CanClaim_TracksAccrualState()
        {
            (TileService service, _, _, _) = NewLoop();
            TileState tile = service.GrantTileFromMatchWin(TileRank.T1, T0, id: "07");

            Assert.IsFalse(service.CanClaim(tile), "Fresh tile has nothing to claim.");

            service.RefreshAccrual(T0.AddHours(2));
            Assert.IsTrue(service.CanClaim(tile), "20 Stone accrued -> claimable.");

            service.Claim(tile, T0.AddHours(2));
            Assert.IsFalse(service.CanClaim(tile), "Post-claim tile is empty again.");
        }

        [Test]
        public void Claim_WithoutPriorRefresh_StillClaimsEverythingEarned()
        {
            // A tap should claim everything up to "now" even if RefreshAccrual was not called
            // first (e.g. the tile filled while the app was closed).
            (TileService service, _, Wallet wallet, _) = NewLoop();
            TileState tile = service.GrantTileFromMatchWin(TileRank.T2, T0, id: "11");

            ClaimResult result = service.Claim(tile, T0.AddHours(4)); // 4h * 25/hr = 100

            Assert.IsTrue(result.Claimed);
            Assert.AreEqual(100, result.StoneClaimed);
            Assert.AreEqual(100, wallet.GetBalance(CurrencyType.Stone));
        }

        [Test]
        public void GrantTileFromMatchWin_IsTheOnlyTileSource_AndEmitsTileAcquired()
        {
            (TileService service, PlayerState state, _, RecordingAnalytics fx) = NewLoop();

            Assert.AreEqual(0, state.Tiles.Count, "No tiles exist before a win.");
            TileState tile = service.GrantTileFromMatchWin(TileRank.T1, T0);

            Assert.AreEqual(1, state.Tiles.Count);
            Assert.AreSame(tile, state.Tiles[0]);
            Assert.AreEqual(1, fx.CountOf(Events.TileAcquired));
        }
    }
}
