namespace Keepfall.Core.Config
{
    /// <summary>
    /// Canonical remote-config key names, in dot.case. These MUST match the keys in
    /// <c>config/remote-config.defaults.json</c> (owned by the config agent) and the bundled
    /// copy at <c>Assets/Resources/remote-config.defaults.json</c>. Every value traces to
    /// source-of-truth: tile yield/cap (§2), accelerator caps/prices (§6 Product 1), AI tier
    /// thresholds (§4), funnel frequency caps (§8), and the Plus trial flag (§6 Product 2).
    /// </summary>
    public static class RemoteConfigKeys
    {
        // ── Tile yield rates per rank (Stone/hour) — §2 ──────────────────
        public const string TileYieldT1 = "tile.yield.t1";
        public const string TileYieldT2 = "tile.yield.t2";
        public const string TileYieldT3 = "tile.yield.t3";

        // ── Tile pre-claim caps (Stone) — §2 ─────────────────────────────
        public const string TileCapT1 = "tile.cap.t1";
        public const string TileCapT2 = "tile.cap.t2";
        public const string TileCapT3 = "tile.cap.t3";

        // ── Accelerator prices (Shards) — §6 Product 1 ───────────────────
        public const string AcceleratorPriceT1 = "accelerator.price.t1";
        public const string AcceleratorPriceT2 = "accelerator.price.t2";
        public const string AcceleratorPriceT3 = "accelerator.price.t3";

        // ── Accelerator hard caps — §6 Product 1 ─────────────────────────
        public const string AcceleratorMaxDaysQueued = "accelerator.maxQueuedDays";
        public const string AcceleratorMinFillPercentToShow = "accelerator.minFillPctToOffer";
        public const string AcceleratorD1LockMinutes = "accelerator.lockedFirstMinutesD1";
        public const string AcceleratorMaxDaysPerPurchase = "accelerator.maxDaysPerPurchase";

        // ── AI difficulty thresholds (roster size that advances tier) — §4 ──
        public const string AiThresholdApprentice = "ai.threshold.apprentice";
        public const string AiThresholdAdept = "ai.threshold.adept";
        public const string AiThresholdTactician = "ai.threshold.tactician";
        public const string AiThresholdCommander = "ai.threshold.commander";
        public const string AiThresholdMarshal = "ai.threshold.marshal";

        // ── Funnel frequency caps — §8 ───────────────────────────────────
        public const string FunnelPlusMaxReveals = "funnel.plus.maxReveals";
        public const string FunnelPlusCooldownDays = "funnel.plus.windowDays";
        public const string FunnelPlusPostD30PerMonth = "funnel.plus.postD30PerMonth";
        public const string FunnelAcceleratorHintCooldownDays = "funnel.accelHint.cooldownDays";
        public const string FunnelAccelHintMaxPerTilePerWeek = "funnel.accelHint.maxPerTilePerWeek";
        public const string FunnelRetryLossStreakRequired = "funnel.retry.minConsecutiveLosses";
        public const string FunnelPostD30SuppressNewTriggers = "funnel.postD30.suppressNewTriggers";

        // ── Keepfall Plus — §6 Product 2 ─────────────────────────────────
        public const string PlusTrialEnabled = "plus.trial.enabled";
        public const string PlusTrialDays = "plus.trial.days";
        public const string PlusYieldBonusPct = "plus.yieldBonusPct";
        public const string PlusExtraDeckSlots = "plus.extraDeckSlots";
    }
}
