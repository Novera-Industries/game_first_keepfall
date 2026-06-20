using Keepfall.Core.Config;
using Keepfall.Core.State;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Confirms remote-config defaults parse to the canonical source-of-truth values, that
    /// runtime overrides take precedence, and that the typed helpers return the right numbers.
    /// </summary>
    public sealed class RemoteConfigTests
    {
        private const string DefaultsJson = @"{
            ""_comment"": ""ignored"",
            ""tile.yield.t1"": 10.0,
            ""tile.yield.t3"": 60.0,
            ""accelerator.price.t3"": 60,
            ""accelerator.maxQueuedDays"": 3,
            ""funnel.plus.maxReveals"": 3,
            ""funnel.plus.windowDays"": 30,
            ""funnel.retry.minConsecutiveLosses"": 3,
            ""plus.trial.enabled"": true,
            ""plus.yieldBonusPct"": 0.50,
            ""plus.extraDeckSlots"": 1
        }";

        [Test]
        public void Defaults_ReturnCanonicalValues()
        {
            var config = new RemoteConfig(DefaultsJson);

            Assert.AreEqual(10.0, config.GetTileYieldPerHour(TileRank.T1), 1e-9);
            Assert.AreEqual(60.0, config.GetTileYieldPerHour(TileRank.T3), 1e-9);
            Assert.AreEqual(60, config.GetAcceleratorPrice(TileRank.T3));
            Assert.AreEqual(3, config.GetFunnelPlusMaxReveals());
            Assert.IsTrue(config.GetPlusTrialEnabled());
            // The schema stores +50% as a fraction (0.50); the accessor returns the 1.5 multiplier.
            Assert.AreEqual(1.5, config.GetPlusYieldMultiplier(), 1e-9);
        }

        [Test]
        public void RenamedKeys_ResolveToCanonicalSchemaNames()
        {
            // Every key here is the canonical schema name (the OLD divergent names are gone).
            var config = new RemoteConfig(DefaultsJson);

            Assert.AreEqual(3, config.GetAcceleratorMaxDaysQueued(), "accelerator.maxQueuedDays resolves.");
            Assert.AreEqual(30, config.GetFunnelPlusCooldownDays(), "funnel.plus.windowDays resolves.");
            Assert.AreEqual(3, config.GetFunnelRetryLossStreakRequired(),
                "funnel.retry.minConsecutiveLosses resolves.");
            Assert.AreEqual(1, config.GetPlusExtraDeckSlots(), "plus.extraDeckSlots resolves.");

            // A +50% bonus FRACTION (0.50) becomes a 1.5x multiplier — never a raw 0.5x.
            config.ApplyOverridesFromJson(@"{ ""plus.yieldBonusPct"": 0.50 }");
            Assert.AreEqual(1.5, config.GetPlusYieldMultiplier(), 1e-9);
        }

        [Test]
        public void CommentKeys_AreIgnored()
        {
            var config = new RemoteConfig(DefaultsJson);
            // "_comment" must not be readable as a config value.
            Assert.AreEqual("fallback", config.GetString("_comment", "fallback"));
        }

        [Test]
        public void Overrides_WinOverDefaults()
        {
            var config = new RemoteConfig(DefaultsJson);
            config.ApplyOverridesFromJson(@"{ ""tile.yield.t1"": 99.0 }");

            Assert.AreEqual(99.0, config.GetTileYieldPerHour(TileRank.T1), 1e-9,
                "Firebase override must take precedence over the bundled default.");
            // Unrelated default still intact.
            Assert.AreEqual(60.0, config.GetTileYieldPerHour(TileRank.T3), 1e-9);
        }

        [Test]
        public void MissingKey_FallsBackToHardcodedCanonicalDefault()
        {
            // Empty config: every read must still return the source-of-truth fallback.
            var config = new RemoteConfig();

            Assert.AreEqual(25.0, config.GetTileYieldPerHour(TileRank.T2), 1e-9);
            Assert.AreEqual(120, config.GetTileCap(TileRank.T1));
            Assert.AreEqual(15, config.GetAcceleratorPrice(TileRank.T1));
            Assert.AreEqual(3, config.GetAcceleratorMaxDaysQueued());
            Assert.AreEqual(15, config.GetAcceleratorD1LockMinutes());
            Assert.AreEqual(0.30, config.GetAcceleratorMinFillPercentToShow(), 1e-9);
            Assert.IsTrue(config.GetFunnelPostD30SuppressNewTriggers());
        }
    }
}
