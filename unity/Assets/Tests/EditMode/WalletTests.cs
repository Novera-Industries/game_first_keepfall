using Keepfall.Core.Currency;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Locks the wallet invariants (source-of-truth §1): exactly two currencies, never
    /// negative, no partial spends.
    /// </summary>
    public sealed class WalletTests
    {
        [Test]
        public void Add_CreditsBalance_AndRaisesEvent()
        {
            var wallet = new Wallet(new WalletState());
            CurrencyType raisedCurrency = default;
            long raisedBalance = -1;
            wallet.BalanceChanged += (c, b) => { raisedCurrency = c; raisedBalance = b; };

            wallet.Add(CurrencyType.Stone, 100);

            Assert.AreEqual(100, wallet.GetBalance(CurrencyType.Stone));
            Assert.AreEqual(CurrencyType.Stone, raisedCurrency);
            Assert.AreEqual(100, raisedBalance);
        }

        [Test]
        public void TrySpend_Succeeds_WhenAffordable()
        {
            var wallet = new Wallet(new WalletState(stone: 100, shards: 0));

            bool ok = wallet.TrySpend(CurrencyType.Stone, 60);

            Assert.IsTrue(ok);
            Assert.AreEqual(40, wallet.GetBalance(CurrencyType.Stone));
        }

        [Test]
        public void TrySpend_Fails_AndDoesNotMutate_WhenUnaffordable()
        {
            var wallet = new Wallet(new WalletState(stone: 50, shards: 0));

            bool ok = wallet.TrySpend(CurrencyType.Stone, 60);

            Assert.IsFalse(ok);
            Assert.AreEqual(50, wallet.GetBalance(CurrencyType.Stone), "Balance must not change on a failed spend.");
        }

        [Test]
        public void Add_NegativeAmount_Throws()
        {
            var wallet = new Wallet(new WalletState());
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => wallet.Add(CurrencyType.Shards, -1));
        }

        [Test]
        public void UnknownCurrency_IsHardRejected()
        {
            var wallet = new Wallet(new WalletState());
            // Cast an out-of-range value to simulate a "third currency" creeping in.
            var bogus = (CurrencyType)99;
            Assert.Throws<System.ArgumentException>(() => wallet.GetBalance(bogus));
            Assert.Throws<System.ArgumentException>(() => wallet.Add(bogus, 1));
        }

        [Test]
        public void ConstructingFromNegativeState_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => new Wallet(new WalletState(stone: -5, shards: 0)));
        }
    }
}
