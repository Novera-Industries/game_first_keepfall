using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Keepfall.Core.Analytics
{
    /// <summary>
    /// <see cref="IAnalytics"/> that logs events to the Unity console. Used in the Editor and
    /// development builds so the funnel and economy can be observed without a live
    /// GameAnalytics/Firebase backend. The production sink replaces this at the composition
    /// root.
    /// </summary>
    public sealed class DebugAnalytics : IAnalytics
    {
        /// <inheritdoc />
        public void Track(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            if (string.IsNullOrEmpty(evt))
            {
                Debug.LogWarning("[Analytics] Track called with empty event name.");
                return;
            }

            if (props == null || props.Count == 0)
            {
                Debug.Log($"[Analytics] {evt}");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("[Analytics] ").Append(evt).Append(" { ");
            bool first = true;
            foreach (KeyValuePair<string, object> kvp in props)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append(kvp.Key).Append('=').Append(kvp.Value ?? "null");
                first = false;
            }

            sb.Append(" }");
            Debug.Log(sb.ToString());
        }
    }
}
