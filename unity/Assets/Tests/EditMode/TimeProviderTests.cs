using System;
using Keepfall.Core.Time;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Proves the injectable clock supports the "tile yield survives app restart" model
    /// (source-of-truth §2): set time, advance across a simulated close, read the delta.
    /// </summary>
    public sealed class TimeProviderTests
    {
        [TearDown]
        public void TearDown() => GameClock.Reset();

        [Test]
        public void FakeTimeProvider_Advance_MovesForward()
        {
            var start = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
            var clock = new FakeTimeProvider(start);

            clock.AdvanceHours(12);

            Assert.AreEqual(start.AddHours(12), clock.UtcNow);
        }

        [Test]
        public void FakeTimeProvider_RejectsBackwardsAdvance()
        {
            var clock = new FakeTimeProvider();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => clock.Advance(TimeSpan.FromHours(-1)));
        }

        [Test]
        public void GameClock_UsesInjectedProvider_AndSimulatesRestartGap()
        {
            var start = new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.Zero);
            var fake = new FakeTimeProvider(start);
            GameClock.SetProvider(fake);

            DateTimeOffset before = GameClock.UtcNow;
            // Simulate the app being closed for 5 hours.
            fake.AdvanceHours(5);
            DateTimeOffset after = GameClock.UtcNow;

            Assert.AreEqual(TimeSpan.FromHours(5), after - before,
                "GameClock must reflect the injected provider so accrual can use wall-clock delta.");
        }

        [Test]
        public void GameClock_Reset_RestoresSystemProvider()
        {
            GameClock.SetProvider(new FakeTimeProvider());
            GameClock.Reset();
            Assert.IsInstanceOf<SystemTimeProvider>(GameClock.Provider);
        }
    }
}
