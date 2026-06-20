# Keepfall — Operator Runbook (Soft Launch)

> One page for the operator (Chris, CFO). Every number traces to
> [`docs/00-source-of-truth.md`](00-source-of-truth.md). Event names trace to
> [`docs/analytics-taxonomy.md`](analytics-taxonomy.md). Scope: **Phase 1, single-player
> PvE, iOS only, CA/AU/NZ soft launch.** If a value here disagrees with the source of
> truth, the source of truth wins.

---

## 1. Soft-launch flow (TestFlight → App Store Connect → CA/AU/NZ)

1. **Build & sign.** Unity 2023 LTS → iOS Xcode export → upload to **App Store Connect**.
   StoreKit 2 products and the auto-renewable subscription (Keepfall Plus, **one tier,
   $5.99/mo, 7-day free trial behind a remote-config flag**) must be created and in
   "Ready to Submit" state, with **sandbox** receipts validated by the Cloudflare Worker.
2. **TestFlight (internal).** Push to the internal team. Smoke the economic loop, a sandbox
   purchase, a subscription renewal (accelerated sandbox renewal), and one retry-token loss
   → retry → win. Confirm the Worker is the authority for receipts + retries.
3. **TestFlight (external, CA/AU/NZ).** Recruit a small external group in the three launch
   markets. Watch crash-free rate and D1 behavior before any public listing.
4. **Phased App Store release, CA/AU/NZ only.** Submit the listing with **ASO assets**
   (title, subtitle, keywords, screenshots, preview). Use Apple **phased release** (7-day
   ramp) so a bad build never hits 100% at once. **No other territories.**
5. **Remote config check before ramp.** Yield rates, accelerator caps, funnel triggers, AI
   thresholds, and the 7-day-trial flag are all set in Firebase Remote Config and verified
   *before* widening the rollout. No client rebuild should be needed to tune any of these.

---

## 2. KPI dashboard (targets from source-of-truth §9 + §6; events from taxonomy)

Event names are canonical per the analytics taxonomy (its §7 KPI mapping); use those exact
names so the dashboard traces to instrumented events, not invented ones.

| Metric | Target | Feeding analytics events |
| --- | --- | --- |
| D1 retention | **≥ 40%** | `install` + `day_index{is_d1}` (distinct returners ÷ installs) |
| D7 retention | **≥ 18%** | `install` + `day_index{is_d7}` |
| D30 retention | **≥ 8%** | `install` + `day_index{is_d30}` |
| ARPDAU | **≥ $0.25** | `iap_purchase` (server, `usd`) ÷ DAU (`session_start` / `day_index`) |
| ARPPU | **≥ $15** | `iap_purchase` (`usd`) ÷ distinct users with ≥ 1 `iap_purchase` |
| Plus conversion (D14 cohort) | **8–12%** | `plus_subscribe{cohort_day_index ≤ 14}` ÷ D14 cohort; `plus_reveal_shown` for touchpoint attribution |
| Accelerator weekly spenders / DAU | **3–5%** | distinct `accelerator_used` (trailing 7d) ÷ avg DAU |

Supporting health signals on the same dashboard (instrument, watch, do not gate on):
- **Loss-rate by AI tier** (`match_end` outcome by `ai_tier`) — targets: ≤ 25% Apprentice ·
  ~40% Tactician · ~55% Marshal. `difficulty_advanced{roster_size}` confirms difficulty
  advances on **roster**, not days.
- **Funnel trigger health** (`funnel_trigger_fired` / `funnel_trigger_suppressed` /
  `funnel_postd30_suppressed`) — confirms frequency caps hold (Plus reveal ≤ 3 / 30 days;
  accelerator hint ≤ 1 / tile / week; retry offer only after 3 consecutive losses on the same
  match) and that **no new triggers fire after D30**.
- **Crash-free sessions** (from Firebase Crashlytics) — see kill criteria below.

---

## 3. Day-90 decision matrix (read off D30 retention × ARPDAU)

| D30 retention | ARPDAU | Decision |
| --- | --- | --- |
| **≥ 15%** | **≥ $0.40** | **Phase 2 (async PvP) + Phase 3 prep.** |
| **8–14%** | **$0.25–$0.39** | **Phase 2 only** — re-evaluate Phase 3 after global. |
| **< 8%** | any | **Pause.** Refactor core loop or wind down. |
| any | **< $0.25** | **Pause.** Refactor monetization *presentation*. **Do not add PvP.** |

Greenlight rule for Phase 2/3: **D30 ≥ 8% AND ARPDAU ≥ $0.25.** Both must hold. A strong
D30 with weak ARPDAU is a **monetization-presentation** problem, not a reason to build PvP.

---

## 4. What we will NOT do at soft launch (guardrails — see anti-patterns doc)

- No **FOMO or discount modals on app open**. The app opens straight into play.
- No **push notifications selling IAP**.
- No **"buy or lose forever"** time-limited power offers.
- No **paywalled units, tiles, or permanent stat boosts**. Tiles come **only** from winning combat.
- No **second subscription tier** and no **VIP scaling**. One tier, $5.99/mo, forever.
- No **third currency** beyond Stone + Shards.
- No **friend-invite bonuses** (no social graph in Phase 1).
- No **auto-presented Shard packs after D3** — packs live in the Shop tab, always visible, never pushed.
- No **PvP** of any kind shipped — placeholder hooks stay inert and marked.

---

## 5. User acquisition (organic-first, minimal paid)

- **ASO first.** The primary lever. Optimize title, subtitle, keyword field, screenshots, and
  preview for CA/AU/NZ. Iterate copy against store-listing conversion before spending on paid.
- **Minimal paid test.** **$50–$150 / day** split across **Meta + TikTok**, app-install
  objective, CA/AU/NZ geo only. Purpose is to read CPI and early retention signal, not to scale.
  Kill any creative that runs 3× over target CPI (the 3× kill rule).
- **Gifted-influencer test.** A small number of **gifted** (unpaid) creators in the launch
  markets for an organic signal. **No friend-invite or referral bonuses** — that anti-pattern
  does not exist in Phase 1.
- Keep all UA copy in the calm, honest, second-person tone. No shouting, no exclamation points.

---

## 6. Rollback / kill criteria

Trigger one of these and we **halt the phased ramp** (or roll back the build) immediately:

- **Crash-free sessions < 99%** in any 24h window during ramp → pause rollout, hotfix.
- **D1 retention < 30%** on the soft-launch cohort (well under the 40% target) → pause UA,
  diagnose onboarding before spending more.
- **Receipt or retry-token integrity failure** — any client able to retry a win, retry a
  retry, or claim above first-attempt rewards (the Worker is the authority; if it is bypassed,
  this is a stop-ship) → roll back and patch server-side.
- **A guardrail violation reaches production** (e.g. a power offer, a third currency, a
  paywalled unit/tile, an exclamation point in a shipped string) → roll back the offending
  build; the anti-pattern checklist failed and must be re-run.
- **App Store / StoreKit rejection or subscription misconfiguration** → withhold the listing
  until corrected; do not widen territories.

Rollback mechanism: halt Apple phased release, flip the relevant Firebase Remote Config flags
off (trial, funnel triggers, accelerator), and submit a hotfix build through the same
TestFlight → phased-release path above.
