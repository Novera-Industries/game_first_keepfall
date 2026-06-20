using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Keepfall.Analytics;
using Keepfall.Core.Analytics;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;

namespace Keepfall.Monetization
{
    /// <summary>
    /// Outcome of a server-authoritative retry redeem. Mirrors what the Worker returns; the
    /// client renders it but NEVER decides it.
    /// </summary>
    public readonly struct RetryRedeemOutcome
    {
        /// <summary>True only if the SERVER redeemed a token and authorized the retry.</summary>
        public bool Redeemed { get; }

        /// <summary>Server reason code when refused (e.g. "cannot_retry_a_win").</summary>
        public string Reason { get; }

        /// <summary>Deterministic replay seed (identical AI/map/hand). Empty when refused.</summary>
        public string ReplaySeed { get; }

        /// <summary>True for the retried attempt — its rewards are capped at the first attempt
        /// (enforced server-side; this flag only drives honest UI).</summary>
        public bool RewardsCappedToFirstAttempt { get; }

        /// <summary>Server-known token balance after the call.</summary>
        public int TokenBalance { get; }

        /// <summary>Calm, §12-compliant message for the UI.</summary>
        public string Message { get; }

        internal RetryRedeemOutcome(
            bool redeemed, string reason, string replaySeed,
            bool rewardsCapped, int tokenBalance, string message)
        {
            Redeemed = redeemed;
            Reason = reason;
            ReplaySeed = replaySeed;
            RewardsCappedToFirstAttempt = rewardsCapped;
            TokenBalance = tokenBalance;
            Message = message;
        }
    }

    /// <summary>
    /// PvE Retry Tokens — source-of-truth §6 Product 3. A CONVENIENCE wrapper over
    /// <see cref="IBackendClient"/>. The Cloudflare Worker is the AUTHORITY: the rules
    /// "cannot retry a win", "cannot retry a retry", and "rewards capped at first attempt" are
    /// enforced server-side. <b>This client never locally authorizes a redeem.</b>
    /// <see cref="RedeemAsync"/> calls the Worker and respects its verdict verbatim.
    ///
    /// <para>Responsibilities: show the locally-cached balance, request the daily login grant
    /// (server caps 3 F2P / 5 Plus), and buy tokens with Shards (1 for 20, or 5 for 90). The
    /// retry OFFER itself is gated by the funnel engine (only after 3 consecutive losses on the
    /// same match, never on first loss, §8); this client exposes the balance/streak it needs.</para>
    ///
    /// Pure C# (no UnityEngine) so the "no local redeem authority" behaviour is testable.
    /// </summary>
    public sealed class RetryTokenClient
    {
        private readonly IBackendClient _backend;
        private readonly Wallet _wallet;
        private readonly RetryState _state;
        private readonly RemoteConfig _config;
        private readonly IAnalytics _analytics;
        private readonly ITimeProvider _time;

        /// <summary>Tokens granted by the 5-pack purchase. Canonical count (§6).</summary>
        private const int FivePackCount = 5;

        /// <summary>Constructs the client over the backend, wallet, and retry cache.</summary>
        public RetryTokenClient(
            IBackendClient backend,
            Wallet wallet,
            RetryState state,
            RemoteConfig config,
            IAnalytics analytics = null,
            ITimeProvider time = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _analytics = analytics;
            _time = time ?? GameClock.Provider;
        }

        // ── Balance / pricing (convenience reads) ────────────────────────

        /// <summary>Locally-cached token balance. The server is authoritative on every redeem
        /// and grant; this is for display only.</summary>
        public int LocalTokenBalance => _state.TokenCount;

        /// <summary>Shard price for a single retry token. Default 20 (§6).</summary>
        public int SingleCostShards => _config.GetInt("retry.cost.single", 20);

        /// <summary>Shard price for a 5-pack of retry tokens. Default 90 (§6).</summary>
        public int FivePackCostShards => _config.GetInt("retry.cost.fivePack", 90);

        /// <summary>
        /// Consecutive losses on a given match. Drives the funnel's "offer only after 3 losses
        /// on the same match, never on first loss" gate (§8). The funnel engine reads this; the
        /// client just surfaces it.
        /// </summary>
        public int LossStreakFor(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                return 0;
            }

            return _state.PerMatchLossStreak.TryGetValue(matchId, out int streak) ? streak : 0;
        }

        // ── Daily grant (server-capped) ──────────────────────────────────

        /// <summary>
        /// Requests the daily login grant. The SERVER enforces the cap (3 F2P / 5 Plus) and
        /// idempotency (one grant per UTC day); the client just calls and caches the returned
        /// balance. Returns true if a token was granted this call.
        /// </summary>
        public async Task<bool> RequestDailyGrantAsync(CancellationToken cancellationToken = default)
        {
            GrantDailyRetryTokenResponse resp =
                await _backend.GrantDailyRetryTokenAsync(cancellationToken);

            if (resp == null)
            {
                return false;
            }

            // Trust the server's balance verbatim.
            _state.TokenCount = resp.TokenBalance;
            if (resp.Granted)
            {
                _state.LastDailyGrantUtc = _time.UtcNow;
            }

            return resp.Granted;
        }

        // ── Shard purchases (convenience — Shards are premium currency, §1) ──

        /// <summary>
        /// Buys a single retry token for <see cref="SingleCostShards"/> Shards. This spends
        /// premium currency the player already owns; it grants a token, never an outcome — the
        /// player still has to win the retry. Returns true on success.
        /// </summary>
        public bool BuySingleWithShards() => BuyWithShards(1, SingleCostShards);

        /// <summary>
        /// Buys a 5-pack of retry tokens for <see cref="FivePackCostShards"/> Shards. Returns
        /// true on success.
        /// </summary>
        public bool BuyFivePackWithShards() => BuyWithShards(FivePackCount, FivePackCostShards);

        private bool BuyWithShards(int tokenCount, int priceShards)
        {
            if (!_wallet.TrySpend(CurrencyType.Shards, priceShards))
            {
                return false;
            }

            // Optimistically reflect the purchased tokens locally for immediate UI feedback. The
            // server stays authoritative: the next redeem/grant reconciles this balance, and the
            // Worker — not this number — decides whether any redeem is allowed.
            _state.TokenCount += tokenCount;

            _analytics?.Track(Events.ShardPackPurchase, new Dictionary<string, object>
            {
                ["pack"] = tokenCount == 1 ? "retry_token_single" : "retry_token_5pack",
                ["price_shards"] = priceShards,
                ["tokens"] = tokenCount,
            });

            return true;
        }

        // ── Redeem (SERVER AUTHORITY — never decided locally) ────────────

        /// <summary>
        /// Asks the Worker whether a match may be retried. Pure query — does not spend a token.
        /// The Worker enforces "cannot retry a win" / "cannot retry a retry" (§6); the client
        /// only renders what is returned.
        /// </summary>
        public async Task<RequestRetryTokenResponse> CheckEligibilityAsync(
            string attemptId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attemptId))
            {
                throw new ArgumentException("attemptId is required.", nameof(attemptId));
            }

            RequestRetryTokenResponse resp = await _backend.RequestRetryTokenAsync(
                new RequestRetryTokenRequest { AttemptId = attemptId }, cancellationToken);

            if (resp != null)
            {
                _state.TokenCount = resp.TokenBalance;
            }

            return resp;
        }

        /// <summary>
        /// Redeems a retry token for <paramref name="attemptId"/> by calling the Worker. The
        /// client does NOT pre-authorize: it does not check win/retry rules, does not require a
        /// positive local balance, and does not debit locally. It submits the request and
        /// returns the SERVER'S verdict. On a redeemed verdict it caches the server balance and
        /// resets the match's loss streak; on a refusal it surfaces a calm mapped message.
        /// </summary>
        public async Task<RetryRedeemOutcome> RedeemAsync(
            string attemptId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attemptId))
            {
                throw new ArgumentException("attemptId is required.", nameof(attemptId));
            }

            // NOTE: deliberately NO local authorization here. The Worker is the authority.
            RedeemRetryTokenResponse resp = await _backend.RedeemRetryTokenAsync(
                new RedeemRetryTokenRequest { AttemptId = attemptId }, cancellationToken);

            if (resp == null)
            {
                return new RetryRedeemOutcome(false, "no_response", string.Empty, false,
                    _state.TokenCount, MonetizationStrings.RetryRefusedGeneric);
            }

            // Trust the server balance verbatim.
            _state.TokenCount = resp.TokenBalance;

            if (!resp.Redeemed)
            {
                return new RetryRedeemOutcome(false, resp.Reason, string.Empty, false,
                    resp.TokenBalance, MapRefusal(resp.Reason));
            }

            // Server authorized the retry: clear this match's loss streak so the offer does not
            // immediately re-fire, and log the server-authoritative redeem.
            _state.PerMatchLossStreak.Remove(matchId);

            _analytics?.Track(Events.RetryTokenRedeemed, new Dictionary<string, object>
            {
                ["match_id"] = matchId,
                ["rewards_capped"] = resp.RewardsCappedToFirstAttempt,
                ["token_balance"] = resp.TokenBalance,
            });

            return new RetryRedeemOutcome(true, resp.Reason, resp.ReplaySeed,
                resp.RewardsCappedToFirstAttempt, resp.TokenBalance,
                MonetizationStrings.RetryOffer);
        }

        // ── Loss-streak bookkeeping (for the §8 funnel gate) ─────────────

        /// <summary>
        /// Records a loss on <paramref name="matchId"/>, incrementing its consecutive-loss
        /// streak. The funnel engine reads <see cref="LossStreakFor"/> to honor "offer only
        /// after 3 consecutive losses on the same match" (§8). Returns the new streak.
        /// </summary>
        public int RecordLoss(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                return 0;
            }

            int streak = LossStreakFor(matchId) + 1;
            _state.PerMatchLossStreak[matchId] = streak;
            return streak;
        }

        /// <summary>Clears a match's loss streak (on a win, or after a successful redeem).</summary>
        public void ResetLossStreak(string matchId)
        {
            if (!string.IsNullOrEmpty(matchId))
            {
                _state.PerMatchLossStreak.Remove(matchId);
            }
        }

        private static string MapRefusal(string reason)
        {
            switch (reason)
            {
                case "cannot_retry_a_win":
                    return MonetizationStrings.RetryRefusedWin;
                case "cannot_retry_a_retry":
                    return MonetizationStrings.RetryRefusedRetry;
                case "insufficient_tokens":
                    return MonetizationStrings.RetryRefusedNoTokens;
                default:
                    return MonetizationStrings.RetryRefusedGeneric;
            }
        }
    }
}
