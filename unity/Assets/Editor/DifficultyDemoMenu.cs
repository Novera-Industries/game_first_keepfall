using Keepfall.Combat;
using Keepfall.Core.Analytics;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Monetization;
using UnityEditor;
using UnityEngine;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// Milestone 06 on-ramp (<c>milestone/06-retry-tokens</c>: "difficulty curve locked, retry
    /// plumbing wired", source-of-truth §4/§6/§13). Prints the AI difficulty curve (tier by roster
    /// size, never by days) and runs the server-authoritative retry flow headlessly.
    /// <para>Menu: <b>Keepfall ▸ Combat</b>. AI selection: <see cref="AiController.SelectTier"/>;
    /// retry client: <see cref="RetryTokenClient"/> (the Worker is the authority).</para>
    /// </summary>
    public static class DifficultyDemoMenu
    {
        [MenuItem("Keepfall/Combat/Log Difficulty Curve")]
        public static void LogCurve()
        {
            var config = new RemoteConfig();
            Debug.Log("[Combat] AI difficulty by UNLOCKED-UNIT COUNT (never days). Roster 0..24:");

            int rangeStart = 0;
            AiTier rangeTier = AiController.SelectTier(0, config);
            for (int count = 1; count <= 24; count++)
            {
                AiTier tier = AiController.SelectTier(count, config);
                if (tier != rangeTier)
                {
                    Debug.Log($"[Combat]   roster {rangeStart,2}-{count - 1,2}: {rangeTier}");
                    rangeStart = count;
                    rangeTier = tier;
                }
            }
            Debug.Log($"[Combat]   roster {rangeStart,2}-24: {rangeTier}");
        }

        [MenuItem("Keepfall/Combat/Simulate Retry After 3 Losses")]
        public static void SimulateRetry()
        {
            var config = new RemoteConfig();
            var backend = new EditorFakeBackendClient();
            var wallet = new Wallet(new WalletState(0, 100));
            var state = new RetryState();
            var client = new RetryTokenClient(backend, wallet, state, config, new DebugAnalytics());

            // Daily login grant (server caps 3 F2P / 5 Plus).
            bool granted = client.RequestDailyGrantAsync().GetAwaiter().GetResult();
            Debug.Log($"[Combat] Daily grant: {granted}. Token balance (cached): {client.LocalTokenBalance}.");

            // Three consecutive losses on the same match — the funnel only offers a retry now (§8).
            const string matchId = "match-001";
            int streak = 0;
            for (int i = 0; i < 3; i++)
            {
                streak = client.RecordLoss(matchId);
            }
            Debug.Log($"[Combat] Loss streak on {matchId}: {streak} (retry offer is gated to >= 3).");

            // Eligibility + redeem are SERVER-authoritative; the client only renders the verdict.
            const string attemptId = "attempt-001";
            RequestRetryTokenResponse elig = client.CheckEligibilityAsync(attemptId).GetAwaiter().GetResult();
            Debug.Log($"[Combat] Server eligibility: eligible={elig.Eligible}, balance={elig.TokenBalance}.");

            RetryRedeemOutcome outcome = client.RedeemAsync(attemptId).GetAwaiter().GetResult();
            Debug.Log($"[Combat] Redeem: redeemed={outcome.Redeemed}, replaySeed={outcome.ReplaySeed}, " +
                      $"rewardsCapped={outcome.RewardsCappedToFirstAttempt}. \"{outcome.Message}\"");
            Debug.Log("[Combat] Retry replays the IDENTICAL AI tier, map seed, and starting hand (§6).");
        }
    }
}
