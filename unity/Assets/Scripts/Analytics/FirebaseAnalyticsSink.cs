using System.Collections.Generic;
using System.Globalization;
using Keepfall.Core.Analytics;

namespace Keepfall.Analytics
{
    /// <summary>
    /// Firebase half of the dual sink (source-of-truth §11, taxonomy §0 "Dual sink").
    ///
    /// <para>
    /// Firebase takes the flat <c>snake_case</c> event name from <see cref="Events"/> directly
    /// plus a typed parameter bag. The taxonomy §0 limits are enforced here so we never ship a
    /// payload Firebase silently truncates:
    /// <list type="bullet">
    ///   <item>parameter keys ≤ <see cref="MaxKeyLength"/> (40) chars;</item>
    ///   <item>string values ≤ <see cref="MaxStringValueLength"/> (100) chars;</item>
    ///   <item><c>usd</c> values are sent as a <c>double</c>.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The native <c>Firebase.Analytics</c> calls live behind <c>#if KEEPFALL_FIREBASE</c>,
    /// set at the build composition root once the Firebase Unity SDK is imported. Until then the
    /// sink builds the sanitized <see cref="Firebase.Analytics.Parameter"/> array via the pure,
    /// testable <see cref="BuildParameters"/> path and is otherwise inert, so the project and
    /// EditMode tests compile without the SDK.
    /// </para>
    /// </summary>
    public sealed class FirebaseAnalyticsSink : IAnalytics
    {
        /// <summary>Firebase max parameter-key length (taxonomy §0).</summary>
        public const int MaxKeyLength = 40;

        /// <summary>Firebase max string parameter-value length (taxonomy §0).</summary>
        public const int MaxStringValueLength = 100;

        /// <inheritdoc />
        public void Track(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            if (string.IsNullOrEmpty(evt))
            {
                return;
            }

#if KEEPFALL_FIREBASE
            Firebase.Analytics.Parameter[] parameters = BuildParameters(props);
            if (parameters == null || parameters.Length == 0)
            {
                Firebase.Analytics.FirebaseAnalytics.LogEvent(evt);
            }
            else
            {
                Firebase.Analytics.FirebaseAnalytics.LogEvent(evt, parameters);
            }
#else
            // SDK not present in this build configuration. Intentionally inert.
            _ = props;
#endif
        }

#if KEEPFALL_FIREBASE
        /// <summary>
        /// Converts the typed prop bag into Firebase parameters, applying the §0 limits and the
        /// <c>usd</c>-as-double rule. Kept in one place so both real and (future) test builds
        /// share the same sanitization.
        /// </summary>
        private static Firebase.Analytics.Parameter[] BuildParameters(
            IReadOnlyDictionary<string, object> props)
        {
            if (props == null || props.Count == 0)
            {
                return null;
            }

            var list = new List<Firebase.Analytics.Parameter>(props.Count);
            foreach (KeyValuePair<string, object> kvp in props)
            {
                string key = SanitizeKey(kvp.Key);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // usd is always a double (taxonomy §0), regardless of how the caller boxed it.
                if (key == "usd")
                {
                    list.Add(new Firebase.Analytics.Parameter(key, ToDouble(kvp.Value)));
                    continue;
                }

                switch (kvp.Value)
                {
                    case null:
                        break;
                    case bool b:
                        list.Add(new Firebase.Analytics.Parameter(key, b ? 1L : 0L));
                        break;
                    case int i:
                        list.Add(new Firebase.Analytics.Parameter(key, (long)i));
                        break;
                    case long l:
                        list.Add(new Firebase.Analytics.Parameter(key, l));
                        break;
                    case float f:
                        list.Add(new Firebase.Analytics.Parameter(key, (double)f));
                        break;
                    case double d:
                        list.Add(new Firebase.Analytics.Parameter(key, d));
                        break;
                    default:
                        list.Add(new Firebase.Analytics.Parameter(key, ClampString(kvp.Value.ToString())));
                        break;
                }
            }

            return list.ToArray();
        }

        private static double ToDouble(object value)
        {
            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                default:
                    return double.TryParse(
                        value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double p)
                        ? p
                        : 0d;
            }
        }
#endif

        /// <summary>Trims a parameter key to the Firebase key limit (taxonomy §0). Public/pure
        /// so the limit is unit-coverable without the SDK.</summary>
        public static string SanitizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            return key.Length <= MaxKeyLength ? key : key.Substring(0, MaxKeyLength);
        }

        /// <summary>Trims a string value to the Firebase value limit (taxonomy §0). Public/pure
        /// so the limit is unit-coverable without the SDK.</summary>
        public static string ClampString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= MaxStringValueLength
                ? value
                : value.Substring(0, MaxStringValueLength);
        }
    }
}
