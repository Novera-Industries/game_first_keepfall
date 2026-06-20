using System;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.Currency;
using Keepfall.Core.State;
using Keepfall.Core.Time;
using Keepfall.Monetization;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Guards the server-authority contract for retry tokens (source-of-truth §6 Product 3): the
    /// client must NOT locally authorize a redeem. It calls the Worker and respects its verdict —
    /// even when the local cache says zero tokens, and even when the local cache says it should
    /// be allowed. The Worker, not the client, decides.
    /// </summary>
    public sealed class RetryTokenClientTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        private static RetryTokenClient Build(
            out FakeBackendClient backend, out Wallet wallet, out RetryState state,
            long shards = 1000)
        {
            backend = new FakeBackendClient();
            wallet = new Wallet(new WalletState(0, shards));
            state = new RetryState();
            var config = new RemoteConfig();
            var clock = new FakeTimeProvider(Now);
            return new RetryTokenClient(backend, wallet, state, config, analytics: null, time: clock);
        }

        [Test]
        public async System.Threading.Tasks.Task Redeem_DoesNotAuthorizeLocally_CallsServerAndRespectsVerdict()
        {
            RetryTokenClient client = Build(out FakeBackendClient backend, out _, out RetryState state);

            // Local cache claims ZERO tokens — a locally-authorizing client would refuse here.
            state.TokenCount = 0;

            backend.NextRedeem = new RedeemRetryTokenResponse
            {
                Redeemed = true,
                ReplaySeed = "seed-abc",
                RewardsCappedToFirstAttempt = true,
                TokenBalance = 2,
            };

            RetryRedeemOutcome outcome = await client.RedeemAsync("match-1");

            Assert.AreEqual(1, backend.RedeemCalls, "The client MUST reach the Worker.");
            Assert.IsTrue(outcome.Redeemed, "It respects the server's 'redeemed' verdict.");
            Assert.AreEqual("seed-abc", outcome.ReplaySeed);
            Assert.IsTrue(outcome.RewardsCappedToFirstAttempt);
            Assert.AreEqual(2, client.LocalTokenBalance, "Balance is taken from the server verbatim.");
        }

        [Test]
        public async System.Threading.Tasks.Task Redeem_RespectsServerRefusal_CannotRetryAWin()
        {
            RetryTokenClient client = Build(out FakeBackendClient backend, out _, out RetryState state);

            // Local cache has tokens and might "look" redeemable — but the server says no.
            state.TokenCount = 5;
            backend.NextRedeem = new RedeemRetryTokenResponse
            {
                Redeemed = false,
                Reason = "cannot_retry_a_win",
                TokenBalance = 5,
            };

            RetryRedeemOutcome outcome = await client.RedeemAsync("match-win");

            Assert.AreEqual(1, backend.RedeemCalls);
            Assert.IsFalse(outcome.Redeemed, "Client never overrides a server refusal.");
            Assert.AreEqual("cannot_retry_a_win", outcome.Reason);
            Assert.AreEqual(MonetizationStrings.RetryRefusedWin, outcome.Message);
            Assert.AreEqual(5, client.LocalTokenBalance, "No local token was spent on refusal.");
        }

        [Test]
        public async System.Threading.Tasks.Task DailyGrant_TrustsServerBalanceAndCap()
        {
            RetryTokenClient client = Build(out FakeBackendClient backend, out _, out _);
            backend.NextGrantDaily = new GrantDailyRetryTokenResponse
            {
                Granted = true,
                TokenBalance = 1,
            };

            bool granted = await client.RequestDailyGrantAsync();

            Assert.IsTrue(granted);
            Assert.AreEqual(1, backend.GrantDailyCalls);
            Assert.AreEqual(1, client.LocalTokenBalance);
        }

        [Test]
        public void BuyWithShards_SpendsPremiumCurrency_AtCanonicalPrices()
        {
            RetryTokenClient client = Build(out _, out Wallet wallet, out RetryState state, shards: 200);

            Assert.AreEqual(20, client.SingleCostShards, "Single token = 20 Shards (§6).");
            Assert.AreEqual(90, client.FivePackCostShards, "5-pack = 90 Shards (§6).");

            Assert.IsTrue(client.BuySingleWithShards());
            Assert.AreEqual(180, wallet.GetBalance(CurrencyType.Shards), "20 Shards spent.");
            Assert.AreEqual(1, state.TokenCount);

            Assert.IsTrue(client.BuyFivePackWithShards());
            Assert.AreEqual(90, wallet.GetBalance(CurrencyType.Shards), "90 more Shards spent.");
            Assert.AreEqual(6, state.TokenCount, "5-pack adds five tokens.");
        }

        [Test]
        public void BuyWithShards_RefusesWhenBroke_NoTokensMinted()
        {
            RetryTokenClient client = Build(out _, out Wallet wallet, out RetryState state, shards: 10);

            Assert.IsFalse(client.BuySingleWithShards(), "Cannot afford 20 Shards with 10.");
            Assert.AreEqual(10, wallet.GetBalance(CurrencyType.Shards), "No charge.");
            Assert.AreEqual(0, state.TokenCount, "No token minted on a failed purchase.");
        }

        [Test]
        public void LossStreak_TracksConsecutiveLossesPerMatch_ForTheFunnelGate()
        {
            RetryTokenClient client = Build(out _, out _, out _);

            Assert.AreEqual(1, client.RecordLoss("m"));
            Assert.AreEqual(2, client.RecordLoss("m"));
            Assert.AreEqual(3, client.RecordLoss("m"));
            Assert.AreEqual(3, client.LossStreakFor("m"),
                "Three consecutive losses — the §8 gate for offering a retry.");

            client.ResetLossStreak("m");
            Assert.AreEqual(0, client.LossStreakFor("m"), "A win clears the streak.");
        }
    }
}
