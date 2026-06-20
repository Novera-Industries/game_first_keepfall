namespace Keepfall.Analytics
{
    /// <summary>
    /// Canonical analytics event-name and trigger-id constants for the GameAnalytics +
    /// Firebase dual sink (source-of-truth §11). Every constant here is the FLAT
    /// <c>snake_case</c> identifier defined verbatim in <c>docs/analytics-taxonomy.md</c>
    /// — that doc is the contract; this file is its compile-time mirror so a typo cannot
    /// silently break a dashboard cohort.
    ///
    /// <para>
    /// This is the single canonical event-name registry: every gameplay, economy,
    /// monetization, and funnel emitter routes through these constants. The taxonomy — the
    /// marketing/KPI contract — is <c>snake_case</c>, and the funnel engine and emitters speak
    /// that dialect. Names are STABLE once shipped: renaming a shipped event breaks dashboards,
    /// so add a new constant instead of editing one (taxonomy §0 "Naming").
    /// </para>
    /// </summary>
    public static class Events
    {
        // ── §1 Session / Retention ───────────────────────────────────────
        /// <summary>First app open, once per install (taxonomy §1).</summary>
        public const string Install = "install";

        /// <summary>App foregrounded, new session window opens (taxonomy §1).</summary>
        public const string SessionStart = "session_start";

        /// <summary>App backgrounded / session window closes (taxonomy §1).</summary>
        public const string SessionEnd = "session_end";

        /// <summary>Calendar-day boundary while active — spine of D1/D7/D30 KPIs (taxonomy §1).</summary>
        public const string DayIndex = "day_index";

        /// <summary>Each tutorial step entered/completed (taxonomy §1).</summary>
        public const string TutorialStep = "tutorial_step";

        /// <summary>Tutorial fully completed — the D1 "pure play" gate (taxonomy §1, SoT §8 D1).</summary>
        public const string TutorialComplete = "tutorial_complete";

        // ── §2 Economy (Stone + Shards only, SoT §1) ─────────────────────
        /// <summary>Filled tile claimed; Stone enters wallet (silent claim, taxonomy §2).</summary>
        public const string TileClaimed = "tile_claimed";

        /// <summary>A tile reached its pre-claim Stone cap (taxonomy §2, SoT §2).</summary>
        public const string TileCapReached = "tile_cap_reached";

        /// <summary>Any Stone credited to wallet (taxonomy §2).</summary>
        public const string StoneEarned = "stone_earned";

        /// <summary>Any Stone debited from wallet (taxonomy §2).</summary>
        public const string StoneSpent = "stone_spent";

        /// <summary>A unit becomes owned via Stone spend — never money (taxonomy §2, SoT §10.2).</summary>
        public const string UnitUnlocked = "unit_unlocked";

        /// <summary>A tile becomes owned — ONLY from a PvE win (taxonomy §2, SoT §2).</summary>
        public const string TileAcquired = "tile_acquired";

        /// <summary>Daily quest finished (taxonomy §2).</summary>
        public const string QuestCompleted = "quest_completed";

        /// <summary>Deck slot bought with Stone (taxonomy §2, SoT §5).</summary>
        public const string DeckExpansionPurchased = "deck_expansion_purchased";

        // ── §3 Combat (SoT §4) ────────────────────────────────────────────
        /// <summary>A PvE match begins (taxonomy §3).</summary>
        public const string MatchStart = "match_start";

        /// <summary>A PvE match resolves win/loss (taxonomy §3).</summary>
        public const string MatchEnd = "match_end";

        /// <summary>A loss that extends a consecutive run on the same seed (taxonomy §3).</summary>
        public const string LossStreak = "loss_streak";

        /// <summary>AI tier increases — driven by roster expansion, never raw days (taxonomy §3).</summary>
        public const string DifficultyAdvanced = "difficulty_advanced";

        // ── §4.1 IAP & Shards (SoT §6, §7) ───────────────────────────────
        /// <summary>StoreKit 2 purchase finishes and the Worker validates the receipt
        /// (server-authoritative, taxonomy §4.1).</summary>
        public const string IapPurchase = "iap_purchase";

        /// <summary>Shard pack SKU rendered in the Shop tab (always-visible, taxonomy §4.1).</summary>
        public const string ShardPackView = "shard_pack_view";

        /// <summary>Shard pack purchase validated (taxonomy §4.1).</summary>
        public const string ShardPackPurchase = "shard_pack_purchase";

        /// <summary>Shards credited from a non-IAP source (taxonomy §4.1).</summary>
        public const string ShardEarned = "shard_earned";

        /// <summary>Shards debited (in-game sink, taxonomy §4.1).</summary>
        public const string ShardSpent = "shard_spent";

        // ── §4.2 Yield Accelerator (SoT §6 Product 1) ────────────────────
        /// <summary>Accelerate option becomes visible for a tile (taxonomy §4.2).</summary>
        public const string AcceleratorOfferShown = "accelerator_offer_shown";

        /// <summary>Player confirms; tile filled to cap, Shards debited (taxonomy §4.2).</summary>
        public const string AcceleratorUsed = "accelerator_used";

        // ── §4.3 Battle Pass (SoT §7 — cosmetic-only) ────────────────────
        /// <summary>Pass tab opened or BP reveal banner shown (taxonomy §4.3).</summary>
        public const string BattlepassView = "battlepass_view";

        /// <summary>Premium BP track purchased (taxonomy §4.3).</summary>
        public const string BattlepassPurchase = "battlepass_purchase";

        /// <summary>A tier's cosmetic reward unlocked by progression (taxonomy §4.3).</summary>
        public const string BattlepassTierUnlock = "battlepass_tier_unlock";

        /// <summary>Tier-skip consumable used — no power bundled (taxonomy §4.3, SoT §7).</summary>
        public const string BattlepassTierSkip = "battlepass_tier_skip";

        // ── §4.4 Keepfall Plus (SoT §6 Product 2 — ONE tier) ─────────────
        /// <summary>A Plus reveal surface is presented (capped 3/30d, taxonomy §4.4, SoT §8.1).</summary>
        public const string PlusRevealShown = "plus_reveal_shown";

        /// <summary>7-day free trial begins (trial flag on, taxonomy §4.4).</summary>
        public const string PlusTrialStart = "plus_trial_start";

        /// <summary>Subscription activates, receipt validated by Worker (taxonomy §4.4).</summary>
        public const string PlusSubscribe = "plus_subscribe";

        /// <summary>StoreKit 2 auto-renewal validated by Worker (taxonomy §4.4).</summary>
        public const string PlusRenew = "plus_renew";

        /// <summary>Cancellation / lapse detected by Worker. Cosmetics kept (taxonomy §4.4, SoT §6).</summary>
        public const string PlusCancel = "plus_cancel";

        // ── §4.5 PvE Retry Tokens (server-authoritative, SoT §6 Product 3) ──
        /// <summary>Retry offer surfaced — only after 3 same-match losses (taxonomy §4.5).</summary>
        public const string RetryOfferShown = "retry_offer_shown";

        /// <summary>A token is sourced (login/Plus/BP/purchase). Server-emitted (taxonomy §4.5).</summary>
        public const string RetryTokenGranted = "retry_token_granted";

        /// <summary>Worker authorizes a retry and restores one attempt (taxonomy §4.5).</summary>
        public const string RetryTokenRedeemed = "retry_token_redeemed";

        // ── §5 Funnel (engine instrumentation, SoT §8) ───────────────────
        /// <summary>The engine decides a trigger's precondition + cap pass and presents it
        /// (taxonomy §5).</summary>
        public const string FunnelTriggerFired = "funnel_trigger_fired";

        /// <summary>The engine evaluates a trigger and declines to show it (taxonomy §5).</summary>
        public const string FunnelTriggerSuppressed = "funnel_trigger_suppressed";

        /// <summary>A non-converter crosses D30; engine hard-branches off all new triggers.
        /// Emitted once at the boundary (taxonomy §5, SoT §8.2).</summary>
        public const string FunnelPostD30Suppressed = "funnel_postd30_suppressed";
    }

    /// <summary>
    /// The <c>trigger_id</c> string registry the funnel engine consumes (taxonomy §6). Each id
    /// maps a SoT §8 day to a concrete trigger whose PRECONDITION is player STATE (the
    /// wall-clock day is the expected correlation, not the gate — SoT §8). These strings travel
    /// on <see cref="Events.FunnelTriggerFired"/> / <see cref="Events.FunnelTriggerSuppressed"/>
    /// and on the matching surface event (e.g. <see cref="Events.PlusRevealShown"/>), so
    /// per-trigger conversion is a join on the same id.
    /// </summary>
    public static class TriggerIds
    {
        /// <summary>D2 — accelerator made discoverable (UI icon only). (taxonomy §6)</summary>
        public const string D2AcceleratorDiscover = "d2_accelerator_discover";

        /// <summary>D3 — $0.99 Shard starter pack, single Shop banner. (taxonomy §6)</summary>
        public const string D3StarterPack = "d3_starter_pack";

        /// <summary>D4–D6 — retry tokens via daily-login drip on the loss screen. (taxonomy §6)</summary>
        public const string D4RetryDrip = "d4_retry_drip";

        /// <summary>D7 — Keepfall Plus first reveal (reveal #1). (taxonomy §6)</summary>
        public const string D7PlusReveal1 = "d7_plus_reveal_1";

        /// <summary>D8–D10 — Battle Pass first cycle reveal. (taxonomy §6)</summary>
        public const string D8Battlepass1 = "d8_battlepass_1";

        /// <summary>D11–D14 — yield accelerator hint near a T3 tile. (taxonomy §6)</summary>
        public const string D11AccelHint = "d11_accel_hint";

        /// <summary>D14 — Plus reveal #2, personalized value framing. (taxonomy §6)</summary>
        public const string D14PlusReveal2 = "d14_plus_reveal_2";

        /// <summary>D15–D21 — retry nudge after 3 consecutive same-match losses. (taxonomy §6)</summary>
        public const string D15RetryNudge = "d15_retry_nudge";

        /// <summary>D22–D28 — Battle Pass second cycle reveal. (taxonomy §6)</summary>
        public const string D22Battlepass2 = "d22_battlepass_2";

        /// <summary>D22–D28 — Plus reveal #3 (final), only if #1 and #2 dismissed. (taxonomy §6)</summary>
        public const string D22PlusReveal3 = "d22_plus_reveal_3";

        /// <summary>D29–D30 — month-end thanks. No sell: free Shard drop + cosmetic. (taxonomy §6)</summary>
        public const string D29Thanks = "d29_thanks";
    }
}
