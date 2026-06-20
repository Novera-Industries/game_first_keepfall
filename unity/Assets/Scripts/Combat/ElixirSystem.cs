using System;
using Keepfall.Core.Config;

namespace Keepfall.Combat
{
    /// <summary>
    /// In-match elixir economy (source-of-truth §4): regenerates at 1 per second, capped at 10.
    /// Both the regen rate and the cap come from RemoteConfig so they can be tuned without a
    /// rebuild (§11). Pure C# and frame-rate independent — <see cref="Tick"/> takes a delta in
    /// seconds, so it is deterministic for a fixed simulation step and unit-testable in EditMode.
    /// Spending is clamped to available elixir; regen is clamped to the cap.
    /// </summary>
    public sealed class ElixirSystem
    {
        // Canonical defaults (§4). RemoteConfig keys mirror the canonical remote-config schema
        // (config/remote-config.schema.json) verbatim so Firebase overrides reach the client;
        // the GetDouble/GetInt fallbacks keep behaviour stable when a key is absent.
        public const string RegenPerSecondKey = "elixir.regenPerSec";
        public const string CapKey = "elixir.cap";
        public const double DefaultRegenPerSecond = 1.0;
        public const int DefaultCap = 10;

        private readonly double _regenPerSecond;
        private readonly int _cap;
        private double _current;

        /// <summary>Creates the system from RemoteConfig (preferred). Starts full at the cap,
        /// matching Clash-Royale-style match openings; pass <paramref name="startFull"/>=false to
        /// start empty.</summary>
        public ElixirSystem(RemoteConfig config, bool startFull = true)
            : this(
                config?.GetDouble(RegenPerSecondKey, DefaultRegenPerSecond) ?? DefaultRegenPerSecond,
                config?.GetInt(CapKey, DefaultCap) ?? DefaultCap,
                startFull)
        {
        }

        /// <summary>Creates the system with explicit tuning (used by tests).</summary>
        public ElixirSystem(double regenPerSecond, int cap, bool startFull = true)
        {
            if (regenPerSecond < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond));
            }

            if (cap <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cap));
            }

            _regenPerSecond = regenPerSecond;
            _cap = cap;
            _current = startFull ? cap : 0.0;
        }

        /// <summary>Current elixir as a continuous value in [0, cap].</summary>
        public double Current => _current;

        /// <summary>Whole elixir currently available to spend (floor of <see cref="Current"/>).</summary>
        public int Available => (int)Math.Floor(_current + 1e-9);

        /// <summary>The cap (default 10, §4).</summary>
        public int Cap => _cap;

        /// <summary>Regen rate in elixir/second (default 1, §4).</summary>
        public double RegenPerSecond => _regenPerSecond;

        /// <summary>
        /// Advances elixir by <paramref name="deltaSeconds"/> of regen, clamped to the cap.
        /// Negative deltas are ignored (a clock never runs backward inside a match).
        /// </summary>
        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds <= 0)
            {
                return;
            }

            _current += deltaSeconds * _regenPerSecond;
            if (_current > _cap)
            {
                _current = _cap;
            }
        }

        /// <summary>True if at least <paramref name="cost"/> whole elixir is available.</summary>
        public bool CanSpend(int cost) => cost >= 0 && _current + 1e-9 >= cost;

        /// <summary>
        /// Spends <paramref name="cost"/> elixir if affordable. Returns true on success; leaves
        /// elixir untouched and returns false otherwise (callers must check before playing a card).
        /// </summary>
        public bool TrySpend(int cost)
        {
            if (!CanSpend(cost))
            {
                return false;
            }

            _current -= cost;
            if (_current < 0)
            {
                _current = 0;
            }

            return true;
        }
    }
}
