# Keepfall — KPI Dashboard & Conversion-Model Measurability Spec

> Milestone 07 (`milestone/07-funnel-analytics`: "conversion model measurable",
> source-of-truth §13). This is the **measurability spec**: for each Phase-1 KPI, the exact
> analytics events + properties that derive it, the formula, and the tool that computes it;
> then the **conversion-funnel measurability table** that brackets every one of the 11
> funnel triggers fire → suppress → conversion.
>
> Every event name, property, trigger_id, and constant here traces to
> [`00-source-of-truth.md`](./00-source-of-truth.md) (cited inline as `SoT §N`) and matches
> the canonical taxonomy [`analytics-taxonomy.md`](./analytics-taxonomy.md) (its §5 funnel
> events, §6 trigger registry, §7 KPI instrumentation). If a value is not in the source of
> truth, it is not here.
>
> The operator-facing dashboard layout and the **day-90 decision matrix** already live in
> [`operator-runbook.md`](./operator-runbook.md) §2–§3 — this doc is the derivation contract
> behind them; it does **not** restate the decision matrix. Scope: **Phase 1 only** —
> single-player PvE, iOS, soft launch CA/AU/NZ (`SoT §0`, `SoT §13`).

---

## 0. How to read this spec

- **Tool column.** Two sinks (`SoT §11`): **GameAnalytics** (GA) owns revenue (its native
  `business` event from `iap_purchase`) and the resource/progression ledgers; **Firebase**
  owns retention cohorts (`day_index` + `is_d1/is_d7/is_d30` booleans) and the funnel param
  bags. A KPI that needs revenue ÷ a cohort is computed where the join is cheapest and noted
  per row.
- **Authority.** Revenue and server-authoritative facts (`iap_purchase`, `plus_subscribe`,
  `plus_renew`, `retry_token_redeemed`, `funnel_postd30_suppressed`) carry `source:"server"`
  and are emitted by the Cloudflare Worker, so KPIs reflect the authority, not an optimistic
  client (`SoT §6 Product 3`, `SoT §8`, taxonomy §0 "Authority").
- **Ambient fields.** Every event implicitly carries `user_id`, `session_id`, `day_index`,
  `country` (CA/AU/NZ), `platform:"ios"`, `app_version`, `remote_config_version` (taxonomy
  §0). They are not repeated below.

---

## 1. Phase-1 KPI measurability (`SoT §9`)

Each row: the **target** (`SoT §9`), the **events + properties** that derive it, the
**formula**, and the **tool** that computes it. Health-context events are watched, not gated.

### 1.1 D1 retention — target ≥ 40% (`SoT §9`)

- **Events / properties:** `install{country}`; `day_index{day_index, is_d1}`.
- **Formula:** `count(distinct user_id with day_index where is_d1=true) / count(distinct install)`.
- **Tool:** **Firebase** (cohort retention from the `day_index` spine; `is_d1` boolean avoids
  date math).
- **Health context (watch, do not gate):** `tutorial_complete{total_s}`,
  `tutorial_step{status}` — D1 polish and **no monetization friction D1–D2** is the mechanism
  (`SoT §9` D1; `SoT §8` D1 is "None — pure play"). The funnel must show **zero**
  `funnel_trigger_fired` on `day_index=1` (see §2 D1 row).

### 1.2 D7 retention — target ≥ 18% (`SoT §9`)

- **Events / properties:** `install`; `day_index{day_index, is_d7}`.
- **Formula:** `count(distinct user_id @ is_d7=true) / count(distinct install)`.
- **Tool:** **Firebase**.
- **Health context:** `tile_claimed{stone}`, `unit_unlocked{tier, roster_size_after}` —
  tile-economy momentum + roster expansion (`SoT §9` D7 mechanism, `SoT §2` curve).

### 1.3 D30 retention — target ≥ 8% (`SoT §9`)

- **Events / properties:** `install`; `day_index{day_index, is_d30}`.
- **Formula:** `count(distinct user_id @ is_d30=true) / count(distinct install)`.
- **Tool:** **Firebase**.
- **Health context:** `plus_subscribe` / `plus_renew` (subscription value),
  `battlepass_tier_unlock{track}` (BP cosmetic pull) — `SoT §9` D30 mechanism.
- **Greenlight input:** this KPI is one of the two inputs to the day-90 decision matrix
  (the other is ARPDAU); the matrix itself is in [`operator-runbook.md`](./operator-runbook.md) §3.

### 1.4 ARPDAU — target ≥ $0.25 (`SoT §9`)

- **Events / properties:** `iap_purchase{usd, product_type}` (`source:"server"`); DAU from
  `session_start` / `day_index`.
- **Formula:** `sum(iap_purchase.usd over the day) / count(distinct active user_id that day)`.
- **Tool:** **GameAnalytics** for the revenue numerator (native `business` event); DAU is
  read from either sink — GA's daily active or Firebase's `day_index`. Mix skews to
  `battlepass_purchase` + Plus (`plus_subscribe`/`plus_renew`) with light Shards spend
  (`SoT §9` ARPDAU mechanism).
- **Greenlight input:** second input to the day-90 decision matrix (see runbook §3).

### 1.5 ARPPU — target ≥ $15 (`SoT §9`)

- **Events / properties:** `iap_purchase{usd}` (`source:"server"`).
- **Formula:** `sum(usd) / count(distinct user_id with ≥ 1 iap_purchase)`.
- **Tool:** **GameAnalytics** (revenue ÷ distinct payers). Anchored by the
  `plus_subscribe`/`plus_renew` cohort (`SoT §9`: "Plus subscribers anchor this number").

### 1.6 Plus conversion, D14 cohort — target 8–12% (`SoT §9`)

- **Events / properties:** cohort from `install` / `day_index`; conversion from
  `plus_subscribe{cohort_day_index, is_from_trial}` and (trial path) `plus_trial_start`.
  Two-touchpoint attribution from `plus_reveal_shown{reveal_index, trigger_id}` joined to
  `funnel_trigger_fired{trigger_id ∈ d7_plus_reveal_1, d14_plus_reveal_2, d22_plus_reveal_3}`.
- **Formula:** numerator = users in the install cohort with `plus_subscribe` where
  `cohort_day_index ≤ 14`; denominator = cohort installs that reached D14
  (`day_index where is_d14`-equivalent / `day_index ≥ 14`). Result =
  `numerator / denominator`.
- **Tool:** **Firebase** for the D14 cohort (retention spine) joined with **GameAnalytics**
  `plus_subscribe` revenue (server-authoritative). The `trigger_id` join attributes which of
  the two reveal touchpoints (`SoT §9`: "Two reveal touchpoints + concrete value framing")
  drove conversion.

### 1.7 Accelerator weekly spenders / DAU — target 3–5% (`SoT §9`)

- **Events / properties:** `accelerator_used{tile_id, shards}`; DAU from
  `session_start` / `day_index`. Tuning funnel from `accelerator_offer_shown{placement}`.
- **Formula:** `count(distinct user_id with ≥ 1 accelerator_used in trailing 7 days) / avg DAU over that week`.
- **Tool:** **GameAnalytics** (resource sink for `accelerator_used`) ÷ DAU. The
  `accelerator_offer_shown → accelerator_used` ratio tunes the "light, contextual prompts
  only" target (`SoT §9` accelerator mechanism; `SoT §8.2` hint caps).

---

## 2. Conversion-funnel measurability (`SoT §8`)

The 11 funnel triggers (`SoT §8` day table; ids in
[`Events.cs` `TriggerIds`](../unity/Assets/Scripts/Analytics/Events.cs)). Each row gives the
**precondition (player STATE per `SoT §8`** — the day is the *expected correlation*, never the
gate), **placement**, **frequency cap** (`SoT §8.2`), the bracketing
`funnel_trigger_fired` / `funnel_trigger_suppressed` events, and the **downstream conversion
event** that closes the loop (the join key is the shared `trigger_id`). This mirrors taxonomy
§6 — it does not contradict it.

| `trigger_id` | Precondition (player STATE, `SoT §8`) | Placement | Frequency cap (`SoT §8.2`) | Brackets (fired / suppressed) | Conversion event (closes the loop) |
| --- | --- | --- | --- | --- | --- |
| `d2_accelerator_discover` | Player has **waited on tile yield ≥ once** (`HasWaitedOnTileYield`). Icon only, not an offer. | `tile_screen` | Once; then the accelerator-hint cap governs. Never in the D1 first-15-min lock. | `funnel_trigger_fired{trigger_id=d2_accelerator_discover}` / `funnel_trigger_suppressed{reason=precondition_unmet\|freq_cap_hit}` | `accelerator_offer_shown` → `accelerator_used` |
| `d3_starter_pack` | **First unlock outside the 6 starters** (`HasUnlockedOutsideStarters`) **AND** first Stone wall (`HasHitStoneWall`). Both legs required. | `shop_tab` (single banner) | Once. Shard packs otherwise live only in the Shop, **never auto-presented after D3** (`SoT §7`). | `funnel_trigger_fired{d3_starter_pack}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit}` | `shard_pack_view` → `shard_pack_purchase` / `iap_purchase{product_type=shard_pack}` |
| `d4_retry_drip` | **Lost ≥ 1 Adept (tier 2) match** (`HasLostAdeptMatch`) **AND** 0 retry tokens (`RetryTokenCount=0`). | `loss_screen` (footer) | Login-granted drip; not within 1 day of a prior drip; not on a first-ever loss with tokens in hand. | `funnel_trigger_fired{d4_retry_drip}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit}` | `retry_token_granted{source=daily_login}` → `retry_offer_shown` |
| `d7_plus_reveal_1` | Week-1 checkpoint **AND unlock pacing slowed** (`UnlockPacingSlowed`). Not if already Plus. | `shop_tab` (single banner) | Plus reveal #1 of **max 3 / 30 days** (`SoT §8.1`). | `funnel_trigger_fired{d7_plus_reveal_1}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit\|already_converted}` | `plus_reveal_shown{reveal_index=1}` → `plus_trial_start` / `plus_subscribe` |
| `d8_battlepass_1` | **Engaged & exploring synergies** (`IsExploringSynergies`) AND first BP cycle mid/complete. | `pass_tab` | Once per cycle surfacing. BP is cosmetic-only. | `funnel_trigger_fired{d8_battlepass_1}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit}` | `battlepass_view{placement=d8_banner}` → `battlepass_purchase` |
| `d11_accel_hint` | Owns a **T3 tile** (`OwnsT3Tile`) **AND** faces a **specialist Stone wall** (`FacesSpecialistWall`) **AND** a candidate tile id exists. | `tile_screen` (tile UI hint) | **Max 1 per tile per week**; **never** if an accelerator was used in the past 7 days (`SoT §8.2`). | `funnel_trigger_fired{d11_accel_hint}` / `funnel_trigger_suppressed{precondition_unmet\|recently_used_accelerator\|freq_cap_hit}` | `accelerator_offer_shown{placement=tile_ui_hint}` → `accelerator_used` |
| `d14_plus_reveal_2` | Two-week checkpoint AND **reveal #1 happened and did not convert** (`Funnel.PlusRevealCount ≥ 1`, not Plus). Personalized framing. | `profile` | Plus reveal #2 of max 3 / 30 days. | `funnel_trigger_fired{d14_plus_reveal_2}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit\|already_converted}` | `plus_reveal_shown{reveal_index=2}` → `plus_trial_start` / `plus_subscribe` |
| `d15_retry_nudge` | **3 consecutive losses on the SAME match seed** (`CurrentMatchLossStreak ≥ 3` on `CurrentLossMatchSeed`). | `loss_screen` | Only after 3 consecutive same-match losses; **never on first loss** (`SoT §8.2`). | `funnel_trigger_fired{d15_retry_nudge}` / `funnel_trigger_suppressed{precondition_unmet\|not_first_loss_rule}` | `retry_offer_shown{loss_count≥3}` → `retry_token_redeemed{verdict=granted}` |
| `d22_battlepass_2` | Roster **~18–22 / 24** (`18 ≤ RosterSize ≤ 22`) AND second BP cycle available. | `pass_tab` | Once per second-cycle surfacing. | `funnel_trigger_fired{d22_battlepass_2}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit}` | `battlepass_view{placement=d22_banner}` → `battlepass_purchase` |
| `d22_plus_reveal_3` | Reveals **#1 and #2 both shown / non-converted** (`Funnel.PlusRevealCount ≥ 2`, still not Plus). | `profile` | Plus reveal #3 (final) of max 3 / 30 days. | `funnel_trigger_fired{d22_plus_reveal_3}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit\|already_converted}` | `plus_reveal_shown{reveal_index=3}` → `plus_trial_start` / `plus_subscribe` |
| `d29_thanks` | Month-end checkpoint reached. **No sell.** | `profile` | Once. Carries **no purchase surface**. | `funnel_trigger_fired{d29_thanks}` / `funnel_trigger_suppressed{precondition_unmet\|freq_cap_hit}` | `shard_earned{source=free_drop}` (+ thanks cosmetic grant) — **not a purchase** |

> **D1 is intentionally absent from the table.** `SoT §8` D1 is "None — pure play": no
> trigger has D1 in its eligibility window, so a healthy D1 player produces **zero**
> `funnel_trigger_fired`. The dashboard asserts this (no funnel fire on `day_index=1`) as the
> D1-friction guard behind the D1 retention KPI (§1.1).

### 2.1 Per-trigger funnel-health overlays (derivable, not §9 targets)

For each `trigger_id` (taxonomy §7 overlays):

- **Fire rate** = `funnel_trigger_fired / (funnel_trigger_fired + funnel_trigger_suppressed)`.
- **Conversion** = `matching conversion event / funnel_trigger_fired` (join on `trigger_id`;
  for monetization triggers the conversion event is the §1.6/§1.7 KPI's numerator).
- **Suppression breakdown** = histogram of `funnel_trigger_suppressed.reason`
  (`precondition_unmet` \| `freq_cap_hit` \| `recently_used_accelerator` \| `already_converted`
  \| `not_first_loss_rule` \| `post_d30`).
- **Tool:** **Firebase** (the funnel events live in the Firebase param bags; GA receives the
  `design:` mirror for funnel-health rollups).

---

## 3. Post-D30 non-converter hard branch (`SoT §8.2`)

After D30 a **non-converter** (no Plus, no meaningful spend) sees **NO new triggers** — the
engine returns `None` and emits `funnel_postd30_suppressed` **once at the boundary**
(`SoT §8.2`; implemented as the explicit `FunnelEngine.TryPostD30HardBranch`, not an emergent
side effect).

- **Observed by:** `funnel_postd30_suppressed{day_index≥30, is_converter=false, triggers_now_suppressed:int}`
  (`source:"server"`), with the invariant that the same user produces **zero** subsequent
  `funnel_trigger_fired`.
- **Dashboard check (post-D30 hygiene):** for every user with a `funnel_postd30_suppressed`,
  `count(funnel_trigger_fired after that boundary) = 0`. Plus thereafter is **once per month**
  only (`SoT §8.2`).
- **Converters differ by design:** a converter at D30+ is **not** in the hard branch (no
  `funnel_postd30_suppressed`); their triggers fall through to the normal closed day-windows.
- **Tool:** **Firebase** for the hygiene join; the boundary event is Worker-emitted.

---

## 4. Guardrails the instrumentation must never violate (`SoT §10`)

These are encoded in the schema and asserted by the funnel tests + taxonomy §8 audit hooks.
Fail the PR if any appears:

- **No push selling IAP** (`SoT §10.4`): there is no push-notification event that carries a
  monetization `product_id` or funnel `trigger_id`. The funnel only emits in-app surfaces.
- **No modal on app open** (`SoT §10.5`, `SoT §10.6`): every fired trigger is a single
  **dismissible banner** in a named in-context placement. `FunnelPresentation.IsModal` is
  hard-wired `false`, `IsDismissible` hard-wired `true`, and `FunnelPlacement` is a **closed
  set with no `app_open` value** — so no event can ever carry `placement=app_open`.
- **No FOMO / discount modal** (`SoT §10.5`–`SoT §10.7`): the presentation contract carries
  **no countdown / expiry / "limited time" field**; no trigger is a "buy or lose forever"
  power offer.
- **Sell time, never outcomes** (`SoT §6`): every conversion event compresses earned time
  (accelerator, Plus yield, retry of an attempt the player must still win) — none grants a
  unit, a tile, or a stat. `tile_acquired` requires a `match_seed` (a win); `unit_unlocked`
  carries `stone_cost` only.
- **Exactly two currencies** (`SoT §1`, `SoT §10.9`): the union of all `*_earned`/`*_spent`
  currency surfaces is `{stone, shards}` — no third.
- **One subscription tier** (`SoT §6 Product 2`, `SoT §10.8`): every `plus_*` event is
  tierless (no `tier`/`plan` param).
- **No friend-invite / social** (`SoT §10.10`): no funnel trigger or event references friends
  or invites.

---

## 5. Cross-references (single source per fact — do not duplicate)

| Fact | Canonical home |
| --- | --- |
| KPI targets + mechanisms | [`00-source-of-truth.md`](./00-source-of-truth.md) §9 |
| Funnel day table + frequency caps + hard branch | [`00-source-of-truth.md`](./00-source-of-truth.md) §8 |
| Event names, parameters, trigger registry, KPI joins | [`analytics-taxonomy.md`](./analytics-taxonomy.md) §1–§7 |
| Operator dashboard layout + **day-90 decision matrix** | [`operator-runbook.md`](./operator-runbook.md) §2–§3 |
| Funnel engine (the code that emits these events) | [`FunnelEngine.cs`](../unity/Assets/Scripts/Funnel/FunnelEngine.cs) |
| 30-day funnel walk-through (editor on-ramp) | `Keepfall ▸ Funnel ▸ Simulate 30-Day Funnel` ([`FunnelDemoMenu.cs`](../unity/Assets/Editor/FunnelDemoMenu.cs)) |
