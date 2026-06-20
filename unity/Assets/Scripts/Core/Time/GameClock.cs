using System;

namespace Keepfall.Core.Time
{
    /// <summary>
    /// Static façade over the active <see cref="ITimeProvider"/>. Gameplay code reads
    /// <see cref="UtcNow"/> instead of <c>DateTimeOffset.UtcNow</c> directly, so a test can
    /// swap in a <see cref="FakeTimeProvider"/> once and have the whole simulation obey it.
    /// Defaults to <see cref="SystemTimeProvider"/> in production.
    /// </summary>
    public static class GameClock
    {
        private static ITimeProvider _provider = new SystemTimeProvider();

        /// <summary>The current time source.</summary>
        public static ITimeProvider Provider => _provider;

        /// <summary>Current UTC instant from the active provider.</summary>
        public static DateTimeOffset UtcNow => _provider.UtcNow;

        /// <summary>
        /// Replaces the active time source. Call once at composition root (production) or
        /// in test setup. Passing <c>null</c> resets to the real system clock.
        /// </summary>
        public static void SetProvider(ITimeProvider provider)
        {
            _provider = provider ?? new SystemTimeProvider();
        }

        /// <summary>Restores the default <see cref="SystemTimeProvider"/>. Use in test teardown.</summary>
        public static void Reset()
        {
            _provider = new SystemTimeProvider();
        }
    }
}
