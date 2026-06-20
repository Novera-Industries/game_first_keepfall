namespace Keepfall.Core.Currency
{
    /// <summary>
    /// The complete set of Keepfall currencies. Exactly two values, by canonical contract
    /// (source-of-truth §1). A third currency is a listed anti-pattern (§10.9) — never add
    /// one. <see cref="Wallet"/> hard-rejects any value outside this enum.
    /// </summary>
    public enum CurrencyType
    {
        /// <summary>Soft currency. Earned from tile yield, quests, and claims. Funds unit
        /// unlocks, deck expansion, and minor cosmetics. Never purchased directly.</summary>
        Stone = 0,

        /// <summary>Premium currency. Acquired via StoreKit 2 IAP, drips, and Keepfall Plus.
        /// Funds accelerators, retry tokens, cosmetics, and tier skips.</summary>
        Shards = 1,
    }
}
