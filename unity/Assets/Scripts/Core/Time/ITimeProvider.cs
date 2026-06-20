using System;

namespace Keepfall.Core.Time
{
    /// <summary>
    /// Abstraction over the wall clock. All tile accrual, funnel timing, subscription
    /// period checks, and retry daily-grant math read time through this interface so the
    /// logic is deterministic and unit-testable across simulated "app restarts".
    /// Production uses <see cref="SystemTimeProvider"/>; tests inject
    /// <see cref="FakeTimeProvider"/>.
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>Current UTC instant. Always UTC — never local time — so accrual is
        /// timezone- and DST-stable.</summary>
        DateTimeOffset UtcNow { get; }
    }
}
