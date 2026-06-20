using System;

namespace Keepfall.Core.Time
{
    /// <summary>
    /// Default <see cref="ITimeProvider"/> backed by the real system clock in UTC.
    /// Pure C# — no UnityEngine dependency — so it works in EditMode and on device alike.
    /// </summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
