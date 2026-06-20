using System;
using Keepfall.Core.Currency;
using Keepfall.Core.Save;
using Keepfall.Core.State;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Round-trips <see cref="PlayerState"/> through <see cref="SaveSystem"/> and confirms
    /// tile-accrual timestamps survive a save/reload (the persistence half of "yield survives
    /// app restart", source-of-truth §2).
    /// </summary>
    public sealed class SaveSystemTests
    {
        [Test]
        public void Load_WhenNothingStored_ReturnsDefaultState()
        {
            var save = new SaveSystem(new InMemorySaveStore());
            PlayerState state = save.Load();

            Assert.IsNotNull(state);
            Assert.AreEqual(PlayerState.CurrentSchemaVersion, state.SchemaVersion);
            Assert.IsNotNull(state.Wallet);
            Assert.IsNotNull(state.Tiles);
        }

        [Test]
        public void SaveThenLoad_PreservesWalletAndTileTimestamps()
        {
            var store = new InMemorySaveStore();
            var save = new SaveSystem(store);

            var original = new PlayerState
            {
                Wallet = new WalletState(stone: 250, shards: 12),
            };
            var wonAt = new DateTimeOffset(2026, 6, 20, 8, 30, 0, TimeSpan.Zero);
            original.Tiles.Add(new TileState("07", TileRank.T2, wonAt) { AccruedStone = 75 });

            save.Save(original);
            PlayerState loaded = save.Load();

            Assert.AreEqual(250, loaded.Wallet.Stone);
            Assert.AreEqual(12, loaded.Wallet.Shards);
            Assert.AreEqual(1, loaded.Tiles.Count);
            Assert.AreEqual("07", loaded.Tiles[0].Id);
            Assert.AreEqual(TileRank.T2, loaded.Tiles[0].Rank);
            Assert.AreEqual(75, loaded.Tiles[0].AccruedStone);
            Assert.AreEqual(wonAt, loaded.Tiles[0].LastAccrualUtc,
                "Accrual anchor must round-trip exactly so wall-clock delta on resume is correct.");
        }

        [Test]
        public void Load_OnCorruptJson_Throws()
        {
            var store = new InMemorySaveStore();
            store.Save("{ this is not valid json ");
            var save = new SaveSystem(store);

            Assert.Throws<SaveCorruptException>(() => save.Load());
        }

        [Test]
        public void Load_OnNewerSchema_Throws()
        {
            var store = new InMemorySaveStore();
            store.Save("{\"schemaVersion\": 9999}");
            var save = new SaveSystem(store);

            Assert.Throws<SaveCorruptException>(() => save.Load());
        }
    }
}
