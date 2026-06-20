using System;
using System.Collections.Generic;
using Keepfall.Core.Analytics;

namespace Keepfall.Analytics
{
    /// <summary>
    /// Production <see cref="IAnalytics"/> router for Keepfall's dual sink: every event is
    /// forwarded to <b>both</b> GameAnalytics and Firebase (source-of-truth §11, taxonomy §0
    /// "Dual sink"). Feature code — and the <see cref="Keepfall.Funnel.FunnelEngine"/> — depend
    /// only on the Core <see cref="IAnalytics"/> contract; this class is the one place that
    /// knows there are two backends.
    ///
    /// <para>
    /// The class is a fan-out over an ordered list of <see cref="IAnalytics"/> sinks. The real
    /// SDK sinks (<see cref="GameAnalyticsSink"/>, <see cref="FirebaseAnalyticsSink"/>) make
    /// their actual SDK calls behind <c>#if KEEPFALL_GAMEANALYTICS</c> / <c>#if
    /// KEEPFALL_FIREBASE</c> so the project compiles and EditMode-tests run without the native
    /// packages imported; when the packages are present those defines are set at the build
    /// composition root and the calls light up. A sink that throws is isolated so one backend
    /// failing never drops the event on the other (telemetry must never crash the game).
    /// </para>
    /// </summary>
    public sealed class AnalyticsService : IAnalytics
    {
        private readonly IReadOnlyList<IAnalytics> _sinks;
        private readonly Action<string> _onSinkError;

        /// <summary>
        /// Builds a router over the given sinks (typically a <see cref="GameAnalyticsSink"/> and
        /// a <see cref="FirebaseAnalyticsSink"/>). <paramref name="onSinkError"/> receives a
        /// human-readable message when a sink throws; pass a logger at the composition root.
        /// </summary>
        public AnalyticsService(IEnumerable<IAnalytics> sinks, Action<string> onSinkError = null)
        {
            if (sinks == null)
            {
                throw new ArgumentNullException(nameof(sinks));
            }

            var list = new List<IAnalytics>();
            foreach (IAnalytics sink in sinks)
            {
                if (sink != null)
                {
                    list.Add(sink);
                }
            }

            _sinks = list;
            _onSinkError = onSinkError;
        }

        /// <summary>
        /// Convenience factory wiring the two canonical Keepfall sinks (GameAnalytics +
        /// Firebase). Both compile to no-ops until their SDK <c>#define</c> is set, so this is
        /// safe to call in any build. <paramref name="onSinkError"/> is forwarded for telemetry
        /// of telemetry failures.
        /// </summary>
        public static AnalyticsService CreateDualSink(Action<string> onSinkError = null)
        {
            return new AnalyticsService(
                new IAnalytics[] { new GameAnalyticsSink(), new FirebaseAnalyticsSink() },
                onSinkError);
        }

        /// <inheritdoc />
        public void Track(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            if (string.IsNullOrEmpty(evt))
            {
                _onSinkError?.Invoke("AnalyticsService.Track called with an empty event name.");
                return;
            }

            // Fan out. Each sink is isolated: a backend failing must not block the others or
            // surface an exception into gameplay code (taxonomy §0 — instrumentation is passive).
            for (int i = 0; i < _sinks.Count; i++)
            {
                try
                {
                    _sinks[i].Track(evt, props);
                }
                catch (Exception ex)
                {
                    _onSinkError?.Invoke(
                        $"Analytics sink '{_sinks[i].GetType().Name}' threw on '{evt}': {ex.Message}");
                }
            }
        }
    }
}
