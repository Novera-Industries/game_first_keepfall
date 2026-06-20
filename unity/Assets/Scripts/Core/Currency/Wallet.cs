using System;

namespace Keepfall.Core.Currency
{
    /// <summary>
    /// Authoritative in-memory wallet service. Wraps a <see cref="WalletState"/> and is the
    /// only place balances change. Guarantees:
    /// <list type="bullet">
    ///   <item>Balances can never go negative.</item>
    ///   <item>Only the two canonical currencies (<see cref="CurrencyType"/>) are accepted;
    ///   any other value throws (defence against a future third-currency anti-pattern).</item>
    ///   <item>Overflow on add is rejected rather than wrapping.</item>
    /// </list>
    /// Pure C# (no UnityEngine) so it runs in EditMode tests.
    /// </summary>
    public sealed class Wallet
    {
        private readonly WalletState _state;

        /// <summary>Raised after any successful balance change. Args: which currency and the
        /// new balance. UI subscribes to refresh counters; analytics subscribes to log.</summary>
        public event Action<CurrencyType, long> BalanceChanged;

        /// <summary>Wraps an existing state (e.g. loaded from save). Never null.</summary>
        public Wallet(WalletState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            if (_state.Stone < 0 || _state.Shards < 0)
            {
                throw new ArgumentException(
                    "Loaded WalletState has a negative balance; save data is corrupt.",
                    nameof(state));
            }
        }

        /// <summary>The underlying state, for serialization. Do not mutate directly.</summary>
        public WalletState State => _state;

        /// <summary>Reads the current balance of a currency.</summary>
        public long GetBalance(CurrencyType currency)
        {
            return currency switch
            {
                CurrencyType.Stone => _state.Stone,
                CurrencyType.Shards => _state.Shards,
                _ => throw UnknownCurrency(currency),
            };
        }

        /// <summary>
        /// Credits <paramref name="amount"/> of <paramref name="currency"/>. Amount must be
        /// &gt;= 0. Rejects overflow. Fires <see cref="BalanceChanged"/> when amount &gt; 0.
        /// </summary>
        public void Add(CurrencyType currency, long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount), "Use TrySpend to remove currency; Add only credits.");
            }

            switch (currency)
            {
                case CurrencyType.Stone:
                    _state.Stone = CheckedAdd(_state.Stone, amount);
                    break;
                case CurrencyType.Shards:
                    _state.Shards = CheckedAdd(_state.Shards, amount);
                    break;
                default:
                    throw UnknownCurrency(currency);
            }

            if (amount > 0)
            {
                BalanceChanged?.Invoke(currency, GetBalance(currency));
            }
        }

        /// <summary>
        /// Attempts to debit <paramref name="amount"/>. Returns <c>false</c> and changes
        /// nothing if the balance is insufficient (no partial spends, no going negative).
        /// Amount must be &gt;= 0. Fires <see cref="BalanceChanged"/> on success when
        /// amount &gt; 0.
        /// </summary>
        public bool TrySpend(CurrencyType currency, long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount), "Spend amount cannot be negative.");
            }

            long balance = GetBalance(currency);
            if (balance < amount)
            {
                return false;
            }

            switch (currency)
            {
                case CurrencyType.Stone:
                    _state.Stone = balance - amount;
                    break;
                case CurrencyType.Shards:
                    _state.Shards = balance - amount;
                    break;
                default:
                    throw UnknownCurrency(currency);
            }

            if (amount > 0)
            {
                BalanceChanged?.Invoke(currency, GetBalance(currency));
            }

            return true;
        }

        /// <summary>True if the wallet can afford <paramref name="amount"/> of the currency.</summary>
        public bool CanAfford(CurrencyType currency, long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return GetBalance(currency) >= amount;
        }

        private static long CheckedAdd(long current, long amount)
        {
            try
            {
                return checked(current + amount);
            }
            catch (OverflowException)
            {
                throw new OverflowException(
                    "Wallet balance overflow; refusing to wrap around to a negative value.");
            }
        }

        private static ArgumentException UnknownCurrency(CurrencyType currency)
        {
            // Reached only if an out-of-range enum value is cast in. This is the hard guard
            // against a third currency ever entering the system through the wallet.
            return new ArgumentException(
                $"Unknown currency '{currency}'. Keepfall has exactly two currencies: " +
                "Stone and Shards (source-of-truth §1).",
                nameof(currency));
        }
    }
}
