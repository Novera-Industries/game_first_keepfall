using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Keepfall.Core.Analytics;

namespace Keepfall.Analytics
{
    /// <summary>
    /// GameAnalytics half of the dual sink (source-of-truth §11, taxonomy §0 "Dual sink").
    ///
    /// <para>
    /// GameAnalytics does not take arbitrary flat event names; it expects typed events. The
    /// taxonomy defines the deterministic mapping (taxonomy §0):
    /// <list type="bullet">
    ///   <item><b>business</b> — real-money IAP. <see cref="Events.IapPurchase"/> routes here.</item>
    ///   <item><b>progression</b> — the AI-tier match ladder. <see cref="Events.MatchStart"/> /
    ///   <see cref="Events.MatchEnd"/> route here (Start / Complete / Fail).</item>
    ///   <item><b>resource</b> — the soft/premium currency ledger (Stone + Shards only, SoT §1).
    ///   The <c>*_earned</c> / <c>*_spent</c> events route here as source/sink flows.</item>
    ///   <item><b>design</b> — everything else, under the colon-delimited hierarchy GA expects
    ///   (e.g. <c>economy:stone_spent:unit_unlock</c>, <c>funnel:funnel_trigger_fired</c>).</item>
    /// </list>
    /// The flat <c>snake_case</c> name in <see cref="Events"/> is the canonical identifier; the
    /// GA hierarchy is derived from it here so the two never drift.
    /// </para>
    ///
    /// <para>
    /// The native <c>GameAnalyticsSDK</c> calls live behind <c>#if KEEPFALL_GAMEANALYTICS</c>.
    /// That define is set at the build composition root once the GA package is imported; until
    /// then this sink is an inert no-op so the project and EditMode tests build without the SDK.
    /// </para>
    /// </summary>
    public sealed class GameAnalyticsSink : IAnalytics
    {
        /// <inheritdoc />
        public void Track(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            if (string.IsNullOrEmpty(evt))
            {
                return;
            }

#if KEEPFALL_GAMEANALYTICS
            // Route by event family per the taxonomy §0 mapping. The GameAnalytics package
            // (GameAnalyticsSDK namespace) exposes NewBusinessEvent / NewProgressionEvent /
            // NewResourceEvent / NewDesignEvent. We pass the canonical snake_case identifier and
            // a flattened fields blob so dashboards keep the full parameter context.
            string fields = BuildFieldsJson(props);

            switch (evt)
            {
                case Events.IapPurchase:
                case Events.ShardPackPurchase:
                case Events.BattlepassPurchase:
                case Events.PlusSubscribe:
                case Events.PlusRenew:
                    // Real money — GA business event. usd/currency come from props.
                    EmitBusiness(evt, props, fields);
                    break;

                case Events.MatchStart:
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(
                        GameAnalyticsSDK.GAProgressionStatus.Start, ProgressionTier(props), fields);
                    break;

                case Events.MatchEnd:
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(
                        IsWin(props)
                            ? GameAnalyticsSDK.GAProgressionStatus.Complete
                            : GameAnalyticsSDK.GAProgressionStatus.Fail,
                        ProgressionTier(props), fields);
                    break;

                case Events.StoneEarned:
                case Events.ShardEarned:
                    EmitResource(GameAnalyticsSDK.GAResourceFlowType.Source, evt, props, fields);
                    break;

                case Events.StoneSpent:
                case Events.ShardSpent:
                    EmitResource(GameAnalyticsSDK.GAResourceFlowType.Sink, evt, props, fields);
                    break;

                default:
                    // Everything else is a GA design event under the colon hierarchy.
                    GameAnalyticsSDK.GameAnalytics.NewDesignEvent(ToDesignPath(evt), fields);
                    break;
            }
#else
            // SDK not present in this build configuration. Intentionally inert.
            _ = props;
#endif
        }

        /// <summary>
        /// Maps a flat <c>snake_case</c> event id to GameAnalytics' colon-delimited
        /// <c>design</c> hierarchy (e.g. <c>funnel_trigger_fired</c> → <c>funnel:trigger_fired</c>,
        /// <c>accelerator_offer_shown</c> → <c>monetization:accelerator_offer_shown</c>). Public
        /// and pure so the mapping is unit-coverable without the SDK present.
        /// </summary>
        public static string ToDesignPath(string evt)
        {
            if (string.IsNullOrEmpty(evt))
            {
                return evt;
            }

            string family = Family(evt);
            return family == null ? evt : family + ":" + evt;
        }

        // Derives the GA top-level category for a design event from its taxonomy section.
        private static string Family(string evt)
        {
            switch (evt)
            {
                case Events.Install:
                case Events.SessionStart:
                case Events.SessionEnd:
                case Events.DayIndex:
                case Events.TutorialStep:
                case Events.TutorialComplete:
                    return "session";

                case Events.TileClaimed:
                case Events.UnitUnlocked:
                case Events.TileAcquired:
                case Events.QuestCompleted:
                case Events.DeckExpansionPurchased:
                    return "economy";

                case Events.LossStreak:
                case Events.DifficultyAdvanced:
                    return "combat";

                case Events.ShardPackView:
                case Events.AcceleratorOfferShown:
                case Events.AcceleratorUsed:
                case Events.BattlepassView:
                case Events.BattlepassTierUnlock:
                case Events.BattlepassTierSkip:
                case Events.PlusRevealShown:
                case Events.PlusTrialStart:
                case Events.PlusCancel:
                case Events.RetryOfferShown:
                case Events.RetryTokenGranted:
                case Events.RetryTokenRedeemed:
                    return "monetization";

                case Events.FunnelTriggerFired:
                case Events.FunnelTriggerSuppressed:
                case Events.FunnelPostD30Suppressed:
                    return "funnel";

                default:
                    return null;
            }
        }

        // Compact, GA-safe field encoding (GA design "fields" is a flat JSON-ish blob). Kept
        // simple and culture-invariant; the rich typed bag is what Firebase consumes.
        private static string BuildFieldsJson(IReadOnlyDictionary<string, object> props)
        {
            if (props == null || props.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder("{");
            bool first = true;
            foreach (KeyValuePair<string, object> kvp in props)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append('"').Append(kvp.Key).Append("\":");
                AppendValue(sb, kvp.Value);
                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append('"').Append(value.ToString().Replace("\"", "\\\"")).Append('"');
                    break;
            }
        }

#if KEEPFALL_GAMEANALYTICS
        private static void EmitBusiness(
            string evt, IReadOnlyDictionary<string, object> props, string fields)
        {
            string currency = GetString(props, "currency", "USD");
            int amountCents = GetUsdCents(props);
            string itemType = GetString(props, "product_type", evt);
            string itemId = GetString(props, "product_id", evt);
            GameAnalyticsSDK.GameAnalytics.NewBusinessEvent(
                currency, amountCents, itemType, itemId, "ios_appstore", fields);
        }

        private static void EmitResource(
            GameAnalyticsSDK.GAResourceFlowType flow, string evt,
            IReadOnlyDictionary<string, object> props, string fields)
        {
            // Two currencies only (SoT §1): the event name carries which one.
            string currency = evt == Events.ShardEarned || evt == Events.ShardSpent
                ? "Shards"
                : "Stone";
            float amount = GetFloat(props, "amount", 0f);
            string itemType = GetString(props, evt == Events.StoneSpent || evt == Events.ShardSpent
                ? "sink"
                : "source", evt);
            GameAnalyticsSDK.GameAnalytics.NewResourceEvent(
                flow, currency, amount, itemType, evt, fields);
        }

        private static string ProgressionTier(IReadOnlyDictionary<string, object> props)
        {
            return "ai_tier_" + GetInt(props, "ai_tier", 1).ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsWin(IReadOnlyDictionary<string, object> props)
        {
            return GetString(props, "result", "loss") == "win";
        }

        private static int GetUsdCents(IReadOnlyDictionary<string, object> props)
        {
            return (int)System.Math.Round(GetFloat(props, "usd", 0f) * 100f);
        }

        private static string GetString(
            IReadOnlyDictionary<string, object> props, string key, string fallback)
        {
            return props != null && props.TryGetValue(key, out object v) && v != null
                ? v.ToString()
                : fallback;
        }

        private static int GetInt(
            IReadOnlyDictionary<string, object> props, string key, int fallback)
        {
            if (props != null && props.TryGetValue(key, out object v) && v != null)
            {
                if (v is int i) return i;
                if (v is long l) return (int)l;
                if (int.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int p))
                {
                    return p;
                }
            }

            return fallback;
        }

        private static float GetFloat(
            IReadOnlyDictionary<string, object> props, string key, float fallback)
        {
            if (props != null && props.TryGetValue(key, out object v) && v != null)
            {
                if (v is float f) return f;
                if (v is double d) return (float)d;
                if (v is int i) return i;
                if (v is long l) return l;
                if (float.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float p))
                {
                    return p;
                }
            }

            return fallback;
        }
#endif
    }
}
