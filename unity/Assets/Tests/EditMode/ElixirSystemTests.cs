using Keepfall.Combat;
using Keepfall.Core.Config;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Verifies the elixir economy (source-of-truth §4): regen 1/sec and cap 10, plus spend
    /// clamping. Uses both the explicit-tuning constructor and the RemoteConfig path.
    /// </summary>
    public sealed class ElixirSystemTests
    {
        [Test]
        public void RegeneratesAtOnePerSecond()
        {
            var elixir = new ElixirSystem(regenPerSecond: 1.0, cap: 10, startFull: false);

            elixir.Tick(1.0);
            Assert.AreEqual(1.0, elixir.Current, 1e-9);

            elixir.Tick(3.5);
            Assert.AreEqual(4.5, elixir.Current, 1e-9);
            Assert.AreEqual(4, elixir.Available, "Available is the floor of Current.");
        }

        [Test]
        public void CapsAtTen()
        {
            var elixir = new ElixirSystem(regenPerSecond: 1.0, cap: 10, startFull: false);

            elixir.Tick(100.0); // way past the cap
            Assert.AreEqual(10.0, elixir.Current, 1e-9);
            Assert.AreEqual(10, elixir.Available);
            Assert.AreEqual(10, elixir.Cap);
        }

        [Test]
        public void DefaultsComeFromRemoteConfigButFallBackToCanonical()
        {
            // Empty config → canonical fallbacks (regen 1, cap 10) per §4.
            var elixir = new ElixirSystem(new RemoteConfig(), startFull: true);
            Assert.AreEqual(1.0, elixir.RegenPerSecond, 1e-9);
            Assert.AreEqual(10, elixir.Cap);
            Assert.AreEqual(10.0, elixir.Current, 1e-9, "startFull seeds at the cap.");
        }

        [Test]
        public void KeysMatchCanonicalRemoteConfigSchema()
        {
            // The elixir keys mirror the canonical schema (config/remote-config.schema.json)
            // so Firebase overrides reach the client (§4, §11).
            Assert.AreEqual("elixir.regenPerSec", ElixirSystem.RegenPerSecondKey);
            Assert.AreEqual("elixir.cap", ElixirSystem.CapKey);
        }

        [Test]
        public void RemoteConfigOverridesTuneRegenAndCap()
        {
            var config = new RemoteConfig();
            config.SetOverride(ElixirSystem.RegenPerSecondKey, 2.0);
            config.SetOverride(ElixirSystem.CapKey, 12);

            var elixir = new ElixirSystem(config, startFull: false);
            Assert.AreEqual(2.0, elixir.RegenPerSecond, 1e-9);
            Assert.AreEqual(12, elixir.Cap);

            elixir.Tick(3.0);
            Assert.AreEqual(6.0, elixir.Current, 1e-9, "regen scales with the configured rate.");
        }

        [Test]
        public void TrySpendDeductsOnlyWhenAffordable()
        {
            var elixir = new ElixirSystem(regenPerSecond: 1.0, cap: 10, startFull: false);
            elixir.Tick(5.0); // 5 elixir

            Assert.IsFalse(elixir.TrySpend(6), "cannot overspend");
            Assert.AreEqual(5.0, elixir.Current, 1e-9, "balance untouched on failure");

            Assert.IsTrue(elixir.TrySpend(4));
            Assert.AreEqual(1.0, elixir.Current, 1e-9);
        }

        [Test]
        public void NegativeAndZeroDeltasDoNotChangeBalance()
        {
            var elixir = new ElixirSystem(regenPerSecond: 1.0, cap: 10, startFull: false);
            elixir.Tick(2.0);
            elixir.Tick(0.0);
            elixir.Tick(-5.0);
            Assert.AreEqual(2.0, elixir.Current, 1e-9, "clock never runs backward inside a match.");
        }
    }
}
