using System.Collections.Generic;

namespace Keepfall.Core.Analytics
{
    /// <summary>
    /// Minimal analytics sink. Feature code calls <see cref="Track"/> with an event name from
    /// <see cref="Events"/> and optional properties. The real GameAnalytics + Firebase
    /// implementation (source-of-truth §11) is a Phase-1 wiring task that lives in the
    /// Analytics feature assembly and implements this interface; Core only owns the contract
    /// and the <see cref="DebugAnalytics"/> logger.
    /// </summary>
    public interface IAnalytics
    {
        /// <summary>
        /// Records an analytics event. <paramref name="evt"/> should be one of the constants
        /// in <see cref="Events"/>. <paramref name="props"/> is optional structured context.
        /// </summary>
        void Track(string evt, IReadOnlyDictionary<string, object> props = null);
    }
}
