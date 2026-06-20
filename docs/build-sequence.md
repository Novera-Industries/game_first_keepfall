# Keepfall — 13-Week Build Sequence (Milestone Branches)

> The 8-milestone plan from [`docs/00-source-of-truth.md`](00-source-of-truth.md) §13.
> Branch names and week ranges are canonical — do not renumber. Files listed are the
> concrete systems each milestone touches in **this** repo. Scope: **Phase 1, single-player
> PvE, iOS only.**

---

## Branch workflow

- `main` is the integration branch. **Branch off `main` per milestone** using the exact
  names below (`milestone/01-economy` … `milestone/08-soft-launch`).
- Work the milestone on its branch, then open a **PR back to `main`**.
- **The guardrail check is a REQUIRED review on every PR.** A reviewer runs
  [`docs/anti-patterns-guardrails.md`](anti-patterns-guardrails.md) top to bottom; any hit
  fails the PR. No merge without a passing guardrail review.
- Every constant a milestone introduces must already exist in the source of truth. If it
  does not, add it there first (in its own PR), then implement.
- **Milestones overlap** (see week ranges). Keep overlapping branches rebased on `main`
  frequently so the shared files below don't drift. Two known overlaps are called out:
  Battle Pass (6–8) overlaps the Accelerator window, and Retry Tokens (9–11) overlaps Plus.

---

## Milestone 01 — Economy

- **Weeks:** 1–3 · **Branch:** `milestone/01-economy`
- **Scope:** Editor-playable economic loop end-to-end: two currencies (Stone, Shards), tile
  yield accrual with wall-clock resume + 12h cap, silent claim, unit-unlock cost ladder.
- **Files / systems:** `unity/Assets/Scripts/Economy/` (Wallet, TileYield, ClaimFlow,
  UnitUnlock), `unity/Assets/Scripts/Data/` (currency + tile + unit-cost ScriptableObjects),
  `config/` (remote-config schema for yield rates + caps), `unity/Assets/Scripts/Core/`
  (wall-clock delta on resume).
- **Definition of done:** A player in the Editor can own tiles, accrue Stone in real time
  (clamped to 12h cap), silently claim, and spend Stone on unit unlocks. Exactly two
  currencies exist. Tiles are grantable **only** by a combat-win event.
- **Tests that must pass:** yield-rate-by-rank (T1 10/120, T2 25/300, T3 60/720),
  12-hour-cap clamp on resume, two-currency invariant (no third currency constructible),
  "no spend path grants a tile" test, daily-Stone-curve tuning check against §2 targets.

## Milestone 02 — Shop + IAP

- **Weeks:** 3–5 · **Branch:** `milestone/02-shop-iap`
- **Scope:** StoreKit 2 Shard packs + Worker-validated receipts. D3 $0.99 starter pack as a
  single banner. Shard packs always visible in Shop, never auto-presented after D3.
- **Files / systems:** `unity/Assets/Scripts/Monetization/` (StoreKit2 client, ShopCatalog,
  ShardPacks), `backend/src/routes/` (receipt-validation route), `backend/src/lib/`
  (StoreKit receipt verification), `backend/src/db/` (purchase ledger in D1).
- **Definition of done:** A sandbox Shard purchase completes, the Worker validates the
  receipt, and Shards land in the wallet. The starter pack shows once on D3 as a single
  banner; packs otherwise live only in the Shop tab.
- **Tests that must pass:** sandbox purchase → Worker validation → wallet credit (happy path),
  duplicate/replayed-receipt rejection, "no auto-present after D3" frequency test, single-banner
  starter-pack test.

## Milestone 03 — Accelerator

- **Weeks:** 5–7 · **Branch:** `milestone/03-accelerator`
- **Scope:** Yield Accelerator consumable — fills one tile to its **current** cap. All §6
  hard caps enforced in code.
- **Files / systems:** `unity/Assets/Scripts/Monetization/` (Accelerator), `unity/Assets/
  Scripts/Economy/` (TileYield hook for instant fill), `config/` (accelerator caps in remote
  config: ≤1 day/purchase, no stacking past 3 days, D1 first-15-min lock, ≥30% fill gate).
- **Definition of done:** Buying an accelerator fills exactly one tile to its current cap,
  never more. Pricing is 15/30/60 Shards for T1/T2/T3. The option is hidden below 30% fill,
  locked in the first 15 minutes of D1, and cannot stack past 3 days queued.
- **Tests that must pass:** "≤ 1 day of yield per purchase" cap, "no stacking past 3 days"
  cap, D1 first-15-min lock, ≥30%-fill gate, price-by-tier (15/30/60).

## Milestone 04 — Battle Pass (overlaps 03)

- **Weeks:** 6–8 (**overlaps the Accelerator window**) · **Branch:** `milestone/04-battlepass`
- **Scope:** First 30-day season, **cosmetic-only** on both tracks, free track completable
  F2P, tier-skip consumable that **never bundles power**.
- **Files / systems:** `unity/Assets/Scripts/Monetization/` (BattlePass, TierSkip),
  `unity/Assets/Scripts/Data/` (season reward table — cosmetics only), `config/` (season
  length + reward definitions).
- **Definition of done:** A 30-day season runs, both tracks award cosmetics only, the free
  track is completable without spend, and a tier skip advances progress without granting any
  power/combat reward. Keep rebased on `main` so shared Monetization files don't diverge from 03.
- **Tests that must pass:** "every Battle Pass reward is a cosmetic" (no power/stat/unit/tile),
  free-track-completable-F2P, "tier skip grants no power" test, 30-day-season-length test.

## Milestone 05 — Keepfall Plus

- **Weeks:** 8–10 · **Branch:** `milestone/05-plus`
- **Scope:** ONE-tier auto-renewable subscription ($5.99/mo, 7-day trial behind RC flag),
  renewal + monthly cosmetic drop flow. **Cosmetics kept on cancellation.**
- **Files / systems:** `unity/Assets/Scripts/Monetization/` (Plus, SubscriptionManager,
  CosmeticDrop), `backend/src/routes/` + `backend/src/lib/` (subscription receipt + renewal
  authority), `unity/Assets/Scripts/Economy/` (+50% yield, +1 deck slot, 2× quest Shards,
  +5 daily login Shards), `unity/Assets/Scripts/Deck/` (4th slot for Plus).
- **Definition of done:** Subscribing applies all six perks (yield +50%, +1 deck slot, 2×
  quest Shards, +5 login Shards, monthly cosmetic, 1 free tier skip/week); a sandbox renewal
  delivers the next cosmetic; cancelling **keeps** every cosmetic already earned. Exactly one
  tier exists. No subscriber-only unit, tile, or combat advantage.
- **Tests that must pass:** **cosmetic-permanence-on-cancellation** (non-negotiable),
  single-tier invariant, "no subscriber-only unit/tile" test, "Plus grants no combat
  advantage" test, sandbox-renewal → cosmetic-drop test.

## Milestone 06 — Retry Tokens (overlaps 05)

- **Weeks:** 9–11 (**overlaps the Plus window**) · **Branch:** `milestone/06-retry-tokens`
- **Scope:** Difficulty curve locked (5 AI tiers, advance on roster), retry-token plumbing
  wired with **server-side authority**.
- **Files / systems:** `unity/Assets/Scripts/Combat/` (AI tiers 1–5, retry replay with
  identical AI/map seed/starting hand), `backend/src/routes/` + `backend/src/lib/` (retry
  authority: cannot retry a win, cannot retry a retry, rewards capped at first attempt),
  `backend/src/db/` (attempt ledger), `config/` (AI thresholds keyed to roster size). Keep
  rebased on `main` so Combat + backend changes stay aligned with Plus's `+1/day cap 5` source.
- **Definition of done:** A loss can be retried once with identical seed/hand/AI; the player
  still must win. Token sources match §6 (daily login 1/day cap 3; Plus +1/day cap 5; BP free
  ~5/season; Shards 20 each or 90 for 5). Difficulty advances on **roster expansion**, never
  raw days. All retry rules are enforced by the **Worker**, not the client.
- **Tests that must pass (server-side):** **cannot-retry-a-win**, **cannot-retry-a-retry**,
  **rewards-capped-at-first-attempt**; client-side: identical-seed/hand/AI replay,
  difficulty-advances-on-roster (not days), token-source caps.

## Milestone 07 — Funnel + Analytics

- **Weeks:** 10–12 · **Branch:** `milestone/07-funnel-analytics`
- **Scope:** 30-day conversion funnel (state-driven triggers), frequency caps, full analytics
  instrumentation so every KPI in §9 is measurable. Hard branch: **no new triggers after D30**.
- **Files / systems:** `unity/Assets/Scripts/Funnel/` (TriggerEngine reading **player state,
  not wall-clock**; FrequencyCaps; D30 hard branch), `unity/Assets/Scripts/Analytics/`
  (GameAnalytics + Firebase events per [`docs/analytics-taxonomy.md`](analytics-taxonomy.md)),
  `config/` (funnel triggers + caps in remote config).
- **Definition of done:** Triggers fire only after the player has felt the relevant friction;
  all frequency caps hold (Plus ≤ 3/30d, accelerator hint ≤ 1/tile/week, retry only after 3
  consecutive losses on the same match); a non-converter sees **no new triggers after D30**;
  every dashboard metric in the runbook has a feeding event.
- **Tests that must pass:** trigger-reads-state-not-clock, each frequency cap, **D30
  no-new-triggers hard branch**, "no FOMO/discount modal on app open", "no IAP push
  notification", KPI-event-coverage (every §9 metric has its event).

## Milestone 08 — Soft Launch

- **Weeks:** 12–13 · **Branch:** `milestone/08-soft-launch`
- **Scope:** TestFlight build, CA/AU/NZ App Store listing, ASO, phased release per the
  [operator runbook](operator-runbook.md).
- **Files / systems:** iOS build config under `unity/ProjectSettings/`, StoreKit 2 product
  config, `config/` (production remote-config values + 7-day-trial flag), store-listing/ASO
  assets, runbook + guardrail docs in `docs/`.
- **Definition of done:** A signed TestFlight build ships to internal then external CA/AU/NZ
  testers; the App Store listing (title/subtitle/keywords/screenshots/preview) is submitted
  for **CA/AU/NZ only** with phased release; remote config is verified before ramp; KPI
  dashboard is live and reading events; kill/rollback criteria are documented and watched.
- **Tests that must pass:** full regression of milestones 01–07 green on `main`; sandbox →
  production StoreKit smoke (purchase, renewal, retry); guardrail checklist clean on the
  release build; crash-free ≥ 99% on the TestFlight external cohort before widening rollout.
