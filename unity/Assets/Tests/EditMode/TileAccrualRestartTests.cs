using System;
using System.Collections.Generic;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.Save;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Economy;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// THE REQUIRED "yield survives app close" test (source-of-truth §2: accrual is real-time
    /// and survives app close, computed from a wall-clock delta and clamped to the 12-hour cap).
    /// Drives a <see cref="FakeTimeProvider"/> to simulate the app being OFF while the wall clock
    /// keeps moving, saves/reloads <see cref="PlayerState"/> mid-way, and asserts accrual resumes
    /// from the persisted anchor with no lost and no double-counted Stone.
    /// </summary>
    public sealed class TileAccrualRestartTests
    {
        private static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

        private RemoteConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new RemoteConfig(); // canonical fallbacks (T1 10/120, etc.)
        }

        [TearDown]
        public void TearDown()
        {
            GameClock.Reset();
        }

        /// <summary>Builds a service over a single freshly-won tile anchored at <see cref="T0"/>.</summary>
        private (TileService service, PlayerState state, Wallet wallet) NewLoopWithOneTile(
            TileRank rank)
        {
            var state = new PlayerState();
            var wallet = new Wallet(state.Wallet);
            var service = new TileService(state.Tiles, wallet, _config, state.Subscription);
            service.GrantTileFromMatchWin(rank, T0, id: "07");
            return (service, state, wallet);
        }

        [Test]
        public void Accrual_ResumesAcrossSimulatedAppClose()
        {
            var clock = new FakeTimeProvider(T0);
            GameClock.SetProvider(clock);

            (TileService service, PlayerState state, _) = NewLoopWithOneTile(TileRank.T1);

            // App open for 2 hours, then a refresh (e.g. before backgrounding).
            clock.AdvanceHours(2);
            service.RefreshAccrual(GameClock.UtcNow);
            Assert.AreEqual(20, state.Tiles[0].AccruedStone, "2h * 10/hr while open.");

            // ---- App CLOSED. Wall clock keeps moving for 3 more hours. No code runs. ----
            clock.AdvanceHours(3);

            // App reopens: a single refresh against the persisted anchor credits the offline gap.
            service.RefreshAccrual(GameClock.UtcNow);
            Assert.AreEqual(50, state.Tiles[0].AccruedStone,
                "Total 5h * 10/hr = 50: the 3h the app was closed must be credited on resume.");
        }

        [Test]
        public void Accrual_WhileClosedPastCap_ClampsToCapOnResume()
        {
            var clock = new FakeTimeProvider(T0);
            GameClock.SetProvider(clock);

            (TileService service, PlayerState state, _) = NewLoopWithOneTile(TileRank.T1);

            // App closed for 30 hours straight (well past the 12h fill). One resume refresh.
            clock.AdvanceHours(30);
            service.RefreshAccrual(GameClock.UtcNow);

            Assert.AreEqual(120, state.Tiles[0].AccruedStone,
                "Offline accrual must clamp to the 12h cap (120), never run unbounded.");
        }

        [Test]
        public void SaveReloadMidway_PreservesContinuity_NoLostOrDoubledStone()
        {
            var store = new InMemorySaveStore();
            var save = new SaveSystem(store);

            // ── Session 1 ──────────────────────────────────────────────
            var clock = new FakeTimeProvider(T0);
            GameClock.SetProvider(clock);

            var s1 = new PlayerState();
            var wallet1 = new Wallet(s1.Wallet);
            var svc1 = new TileService(s1.Tiles, wallet1, _config, s1.Subscription);
            svc1.GrantTileFromMatchWin(TileRank.T2, T0, id: "11"); // 25/hr, 300 cap

            clock.AdvanceHours(4);
            svc1.RefreshAccrual(GameClock.UtcNow);
            Assert.AreEqual(100, s1.Tiles[0].AccruedStone, "4h * 25/hr = 100.");

            // Persist mid-way (anchor is now T0+4h, balance 100) and "quit".
            save.Save(s1);

            // ── App closed for 4 hours of wall-clock time ──────────────
            clock.AdvanceHours(4);

            // ── Session 2: fresh objects loaded from the same save ─────
            PlayerState s2 = save.Load();
            var wallet2 = new Wallet(s2.Wallet);
            var svc2 = new TileService(s2.Tiles, wallet2, _config, s2.Subscription);

            // Anchor round-tripped exactly, so the offline 4h is credited once: 100 + 100 = 200.
            svc2.RefreshAccrual(GameClock.UtcNow);
            Assert.AreEqual(200, s2.Tiles[0].AccruedStone,
                "Reloaded tile resumes from its persisted anchor: total 8h * 25/hr = 200.");

            // A second refresh at the same instant must be idempotent (no double count).
            svc2.RefreshAccrual(GameClock.UtcNow);
            Assert.AreEqual(200, s2.Tiles[0].AccruedStone, "Refresh at same 'now' is idempotent.");

            // Claiming moves exactly the accrued Stone into the wallet and resets the tile.
            ClaimResult result = svc2.Claim(s2.Tiles[0], GameClock.UtcNow);
            Assert.IsTrue(result.Claimed);
            Assert.AreEqual(200, result.StoneClaimed);
            Assert.AreEqual(200, wallet2.GetBalance(CurrencyType.Stone));
            Assert.AreEqual(0, s2.Tiles[0].AccruedStone);
        }

        [Test]
        public void OfflineThenClaim_ThenMoreOffline_AccruesFromClaimAnchor()
        {
            var clock = new FakeTimeProvider(T0);
            GameClock.SetProvider(clock);

            (TileService service, PlayerState state, Wallet wallet) = NewLoopWithOneTile(TileRank.T1);

            // 6h offline, reopen, claim 60 Stone.
            clock.AdvanceHours(6);
            ClaimResult first = service.Claim(state.Tiles[0], GameClock.UtcNow);
            Assert.AreEqual(60, first.StoneClaimed);
            Assert.AreEqual(60, wallet.GetBalance(CurrencyType.Stone));

            // Close again for 6h: accrual restarts from the claim anchor, not from T0.
            clock.AdvanceHours(6);
            service.RefreshAccrual(GameClock.UtcNow);
            Assert.AreEqual(60, state.Tiles[0].AccruedStone,
                "Post-claim accrual is measured from the claim instant, not the original anchor.");
        }

        [Test]
        public void MultipleTiles_DifferentRanks_AccrueIndependentlyAcrossClose()
        {
            var clock = new FakeTimeProvider(T0);
            GameClock.SetProvider(clock);

            var state = new PlayerState();
            var wallet = new Wallet(state.Wallet);
            var service = new TileService(state.Tiles, wallet, _config, state.Subscription);
            service.GrantTileFromMatchWin(TileRank.T1, T0, id: "01"); // 10/hr
            service.GrantTileFromMatchWin(TileRank.T3, T0, id: "02"); // 60/hr, 720 cap

            // Closed 10 hours.
            clock.AdvanceHours(10);
            service.RefreshAccrual(GameClock.UtcNow);

            TileState t1 = state.Tiles.Find(t => t.Id == "01");
            TileState t3 = state.Tiles.Find(t => t.Id == "02");
            Assert.AreEqual(100, t1.AccruedStone, "T1: 10h * 10/hr.");
            Assert.AreEqual(600, t3.AccruedStone, "T3: 10h * 60/hr, still under the 720 cap.");
        }
    }
}
