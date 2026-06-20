using System.Collections.Generic;
using Keepfall.Core.Analytics;

namespace Keepfall.Tests
{
    /// <summary>
    /// Test <see cref="IAnalytics"/> spy that records every tracked event in order. Lets economy
    /// tests assert exactly which events fired (e.g. that a silent claim emits only
    /// <c>tile.claimed</c> and never a celebratory/modal event).
    /// </summary>
    public sealed class RecordingAnalytics : IAnalytics
    {
        /// <summary>A single recorded call.</summary>
        public readonly struct Entry
        {
            /// <summary>Event name.</summary>
            public readonly string Event;

            /// <summary>Properties passed with the event (may be null).</summary>
            public readonly IReadOnlyDictionary<string, object> Props;

            /// <summary>Creates a recorded entry.</summary>
            public Entry(string evt, IReadOnlyDictionary<string, object> props)
            {
                Event = evt;
                Props = props;
            }
        }

        /// <summary>All recorded events, in call order.</summary>
        public readonly List<Entry> Events = new List<Entry>();

        /// <inheritdoc />
        public void Track(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            Events.Add(new Entry(evt, props));
        }

        /// <summary>Count of recorded calls for a given event name.</summary>
        public int CountOf(string evt)
        {
            int n = 0;
            foreach (Entry e in Events)
            {
                if (e.Event == evt)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>True if any recorded event name matches <paramref name="evt"/>.</summary>
        public bool Contains(string evt) => CountOf(evt) > 0;
    }
}
