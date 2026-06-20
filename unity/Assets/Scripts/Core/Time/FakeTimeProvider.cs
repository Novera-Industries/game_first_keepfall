using System;

namespace Keepfall.Core.Time
{
    /// <summary>
    /// Controllable <see cref="ITimeProvider"/> for tests. The clock only moves when the
    /// test moves it, which lets a single test simulate hours of tile accrual or an
    /// "app restart" gap (set time, save, advance, reload) without waiting in real time.
    /// </summary>
    public sealed class FakeTimeProvider : ITimeProvider
    {
        private DateTimeOffset _now;

        /// <summary>Creates a fake clock starting at <paramref name="start"/>.</summary>
        public FakeTimeProvider(DateTimeOffset start)
        {
            _now = start.ToUniversalTime();
        }

        /// <summary>Creates a fake clock starting at the Unix epoch (UTC).</summary>
        public FakeTimeProvider()
            : this(DateTimeOffset.UnixEpoch)
        {
        }

        /// <inheritdoc />
        public DateTimeOffset UtcNow => _now;

        /// <summary>Jumps the clock to an absolute UTC instant.</summary>
        public void SetUtcNow(DateTimeOffset value)
        {
            _now = value.ToUniversalTime();
        }

        /// <summary>Moves the clock forward by <paramref name="delta"/>. Negative deltas
        /// are rejected so a fake clock can never run backwards mid-test.</summary>
        public void Advance(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delta), "FakeTimeProvider cannot move backwards.");
            }

            _now += delta;
        }

        /// <summary>Convenience: advance by whole hours (common for tile-accrual tests).</summary>
        public void AdvanceHours(double hours) => Advance(TimeSpan.FromHours(hours));
    }
}
