using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keepfall.Core.Config
{
    /// <summary>
    /// Typed accessor over remote-config values. Holds two layers: bundled DEFAULTS (parsed
    /// from <c>remote-config.defaults.json</c>) and an OVERRIDE layer pushed at runtime by
    /// Firebase Remote Config (source-of-truth §11). Reads prefer overrides, then defaults,
    /// then the caller-supplied fallback, so the game always has a sane value even offline.
    /// <para>
    /// Pure C# (no UnityEngine): construct it by feeding JSON strings, which keeps it
    /// EditMode-testable. The Unity bootstrap loads the bundled Resources copy and calls
    /// <see cref="LoadDefaultsFromJson"/>, then later <see cref="ApplyOverridesFromJson"/>
    /// when Firebase fetch completes.
    /// </para>
    /// </summary>
    public sealed class RemoteConfig
    {
        private readonly Dictionary<string, JToken> _defaults =
            new Dictionary<string, JToken>(StringComparer.Ordinal);

        private readonly Dictionary<string, JToken> _overrides =
            new Dictionary<string, JToken>(StringComparer.Ordinal);

        /// <summary>Creates an empty config (all reads fall back to caller defaults).</summary>
        public RemoteConfig()
        {
        }

        /// <summary>Creates a config seeded from a bundled-defaults JSON object string.</summary>
        public RemoteConfig(string defaultsJson)
        {
            LoadDefaultsFromJson(defaultsJson);
        }

        /// <summary>Replaces the defaults layer from a flat JSON object (key -> value).</summary>
        public void LoadDefaultsFromJson(string json)
        {
            LoadInto(_defaults, json);
        }

        /// <summary>Merges Firebase-fetched values into the override layer (key -> value).</summary>
        public void ApplyOverridesFromJson(string json)
        {
            LoadInto(_overrides, json);
        }

        /// <summary>Sets a single override value (e.g. from a typed Firebase callback).</summary>
        public void SetOverride(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is required.", nameof(key));
            }

            _overrides[key] = JToken.FromObject(value);
        }

        // ── Generic typed reads ──────────────────────────────────────────

        /// <summary>Reads a double, falling back to <paramref name="defaultValue"/>.</summary>
        public double GetDouble(string key, double defaultValue)
        {
            return TryGet(key, out JToken token) && TryToDouble(token, out double v)
                ? v
                : defaultValue;
        }

        /// <summary>Reads an int, falling back to <paramref name="defaultValue"/>.</summary>
        public int GetInt(string key, int defaultValue)
        {
            return TryGet(key, out JToken token) && TryToInt(token, out int v)
                ? v
                : defaultValue;
        }

        /// <summary>Reads a bool, falling back to <paramref name="defaultValue"/>.</summary>
        public bool GetBool(string key, bool defaultValue)
        {
            return TryGet(key, out JToken token) && TryToBool(token, out bool v)
                ? v
                : defaultValue;
        }

        /// <summary>Reads a string, falling back to <paramref name="defaultValue"/>.</summary>
        public string GetString(string key, string defaultValue)
        {
            return TryGet(key, out JToken token) && token.Type != JTokenType.Null
                ? token.ToString()
                : defaultValue;
        }

        // ── Strongly-typed canonical helpers (values trace to source-of-truth) ──

        /// <summary>Tile yield rate (Stone/hour) for a rank. Defaults: T1=10, T2=25, T3=60 (§2).</summary>
        public double GetTileYieldPerHour(State.TileRank rank)
        {
            return rank switch
            {
                State.TileRank.T1 => GetDouble(RemoteConfigKeys.TileYieldT1, 10.0),
                State.TileRank.T2 => GetDouble(RemoteConfigKeys.TileYieldT2, 25.0),
                State.TileRank.T3 => GetDouble(RemoteConfigKeys.TileYieldT3, 60.0),
                _ => throw new ArgumentOutOfRangeException(nameof(rank)),
            };
        }

        /// <summary>Tile pre-claim Stone cap for a rank. Defaults: T1=120, T2=300, T3=720 (§2).</summary>
        public long GetTileCap(State.TileRank rank)
        {
            return rank switch
            {
                State.TileRank.T1 => GetInt(RemoteConfigKeys.TileCapT1, 120),
                State.TileRank.T2 => GetInt(RemoteConfigKeys.TileCapT2, 300),
                State.TileRank.T3 => GetInt(RemoteConfigKeys.TileCapT3, 720),
                _ => throw new ArgumentOutOfRangeException(nameof(rank)),
            };
        }

        /// <summary>Accelerator price in Shards for a rank. Defaults: T1=15, T2=30, T3=60 (§6).</summary>
        public int GetAcceleratorPrice(State.TileRank rank)
        {
            return rank switch
            {
                State.TileRank.T1 => GetInt(RemoteConfigKeys.AcceleratorPriceT1, 15),
                State.TileRank.T2 => GetInt(RemoteConfigKeys.AcceleratorPriceT2, 30),
                State.TileRank.T3 => GetInt(RemoteConfigKeys.AcceleratorPriceT3, 60),
                _ => throw new ArgumentOutOfRangeException(nameof(rank)),
            };
        }

        /// <summary>Max days of yield that may be queued per tile via accelerator. Default 3 (§6).</summary>
        public int GetAcceleratorMaxDaysQueued() =>
            GetInt(RemoteConfigKeys.AcceleratorMaxDaysQueued, 3);

        /// <summary>Min fill fraction before the accelerate option appears. Default 0.30 (§6).</summary>
        public double GetAcceleratorMinFillPercentToShow() =>
            GetDouble(RemoteConfigKeys.AcceleratorMinFillPercentToShow, 0.30);

        /// <summary>Minutes of D1 play during which the accelerator is locked. Default 15 (§6).</summary>
        public int GetAcceleratorD1LockMinutes() =>
            GetInt(RemoteConfigKeys.AcceleratorD1LockMinutes, 15);

        /// <summary>Max days of yield a single accelerator purchase may grant. Default 1 (§6).</summary>
        public int GetAcceleratorMaxDaysPerPurchase() =>
            GetInt(RemoteConfigKeys.AcceleratorMaxDaysPerPurchase, 1);

        /// <summary>
        /// Roster-size threshold that unlocks an AI tier (§4: difficulty advances on roster
        /// expansion, never raw days). Defaults seeded from the expected D-range correlation.
        /// </summary>
        public int GetAiThreshold(string key, int defaultValue) => GetInt(key, defaultValue);

        /// <summary>Tactician AI threshold (roster size). Convenience for the canonical key.</summary>
        public int GetAiThresholdTactician() =>
            GetInt(RemoteConfigKeys.AiThresholdTactician, 12);

        /// <summary>Max Keepfall Plus reveals per cooldown window. Default 3 / 30 days (§8).</summary>
        public int GetFunnelPlusMaxReveals() =>
            GetInt(RemoteConfigKeys.FunnelPlusMaxReveals, 3);

        /// <summary>Plus reveal rolling-window length in days. Default 30 (§8).</summary>
        public int GetFunnelPlusCooldownDays() =>
            GetInt(RemoteConfigKeys.FunnelPlusCooldownDays, 30);

        /// <summary>Plus reveals allowed per month for a non-converter after D30. Default 1 (§8).</summary>
        public int GetFunnelPlusPostD30PerMonth() =>
            GetInt(RemoteConfigKeys.FunnelPlusPostD30PerMonth, 1);

        /// <summary>Days after a player USES an accelerator during which hints are suppressed.
        /// Default 7 (§8).</summary>
        public int GetFunnelAcceleratorHintCooldownDays() =>
            GetInt(RemoteConfigKeys.FunnelAcceleratorHintCooldownDays, 7);

        /// <summary>Max accelerator hints per tile per week. Default 1 (§8).</summary>
        public int GetFunnelAccelHintMaxPerTilePerWeek() =>
            GetInt(RemoteConfigKeys.FunnelAccelHintMaxPerTilePerWeek, 1);

        /// <summary>Consecutive losses on a match required before a retry nudge. Default 3 (§8).</summary>
        public int GetFunnelRetryLossStreakRequired() =>
            GetInt(RemoteConfigKeys.FunnelRetryLossStreakRequired, 3);

        /// <summary>Whether to suppress all NEW funnel triggers after D30. Default true (§8).</summary>
        public bool GetFunnelPostD30SuppressNewTriggers() =>
            GetBool(RemoteConfigKeys.FunnelPostD30SuppressNewTriggers, true);

        /// <summary>Whether the 7-day Plus free trial is offered. Default true (§6).</summary>
        public bool GetPlusTrialEnabled() =>
            GetBool(RemoteConfigKeys.PlusTrialEnabled, true);

        /// <summary>Length of the Plus free trial in days. Default 7 (§6).</summary>
        public int GetPlusTrialDays() => GetInt(RemoteConfigKeys.PlusTrialDays, 7);

        /// <summary>
        /// Plus tile-yield multiplier on owned tiles. The schema stores a +50% bonus as a
        /// FRACTION (<c>plus.yieldBonusPct</c> = 0.50), so the multiplier is 1 + fraction = 1.5.
        /// Returns 1.5 by default; never reads the 0.50 fraction as a raw 0.5x (§6).
        /// </summary>
        public double GetPlusYieldMultiplier() =>
            1.0 + GetDouble(RemoteConfigKeys.PlusYieldBonusPct, 0.50);

        /// <summary>Extra deck slots granted by Plus. Default 1 (3 -> 4 total, §5/§6).</summary>
        public int GetPlusExtraDeckSlots() =>
            GetInt(RemoteConfigKeys.PlusExtraDeckSlots, 1);

        // ── Internals ────────────────────────────────────────────────────

        private bool TryGet(string key, out JToken token)
        {
            if (_overrides.TryGetValue(key, out token))
            {
                return true;
            }

            return _defaults.TryGetValue(key, out token);
        }

        private static void LoadInto(Dictionary<string, JToken> target, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            JObject obj = JObject.Parse(json);
            foreach (KeyValuePair<string, JToken> kvp in obj)
            {
                // Skip documentation/comment keys (e.g. "_comment") so they never shadow a key.
                if (kvp.Key.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                target[kvp.Key] = kvp.Value;
            }
        }

        private static bool TryToDouble(JToken token, out double value)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    value = token.Value<double>();
                    return true;
                case JTokenType.String:
                    return double.TryParse(
                        token.Value<string>(), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out value);
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryToInt(JToken token, out int value)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                    value = token.Value<int>();
                    return true;
                case JTokenType.Float:
                    value = (int)token.Value<double>();
                    return true;
                case JTokenType.String:
                    return int.TryParse(
                        token.Value<string>(), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out value);
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryToBool(JToken token, out bool value)
        {
            switch (token.Type)
            {
                case JTokenType.Boolean:
                    value = token.Value<bool>();
                    return true;
                case JTokenType.Integer:
                    value = token.Value<long>() != 0;
                    return true;
                case JTokenType.String:
                    return bool.TryParse(token.Value<string>(), out value);
                default:
                    value = false;
                    return false;
            }
        }
    }
}
