# Keepfall — Analytics Event Taxonomy & Funnel Reference

> Deliverable #4 (analytics half). Canonical event taxonomy for **GameAnalytics +
> Firebase**, the **30-day funnel trigger map**, and the **KPI instrumentation** wiring.
>
> Every event, parameter, constant, trigger_id, and copy string here traces back to
> [`00-source-of-truth.md`](./00-source-of-truth.md) (cited inline as `SoT §N`). If a value
> is not in the source of truth, it is not here — add it there first, then instrument it.
>
> Scope: **Phase 1 only** — single-player PvE, iOS (`SoT §0`). No PvP events. No social /
> friend-invite events (`SoT §10.10`). Exactly two currencies in all economy events:
> **Stone** and **Shards** — never a third (`SoT §1`, `SoT §10.9`).

---

## 0. Conventions (read first)

**Naming.** Event names and parameter keys are `snake_case`, present tense, and **stable**
once shipped. Renaming a shipped event breaks dashboards and cohorts — add a new event
instead. Names are the contract the funnel-engine agent and dashboard consume.

**Dual sink.** Every event is sent to **both** GameAnalytics and Firebase unless flagged
otherwise:

- **GameAnalytics** — `design` events use the colon-delimited hierarchy GA expects
  (e.g. `economy:stone_spent:unit_unlock`). The flat `snake_case` name below is the
  canonical identifier; the GA hierarchy is a deterministic mapping documented per group.
  IAP uses GA's native `business` event (real money). Progression uses GA `progression`.
- **Firebase** — flat `snake_case` event name + a typed params bag. Param keys ≤ 40 chars,
  string values ≤ 100 chars (Firebase limits). `usd` values are sent as a `double`.

**Authority.** Client emits gameplay/economy/monetization events. **Server-authoritative**
facts (retry-token validity, receipt validation, post-D30 suppression) are emitted by the
**Cloudflare Worker** so the dashboard reflects the authority, not an optimistic client
(`SoT §6 Product 3`, `SoT §8`). Such events carry `source: "server"`; client-mirrored
copies (if any) carry `source: "client"` and are excluded from revenue/authority KPIs.

**Identity & cohort.** Every event implicitly carries `user_id` (anon install id),
`session_id`, `app_version`, `build`, `platform: "ios"`, `country` (CA/AU/NZ only at soft
launch, `SoT §13`), and `remote_config_version` (Firebase Remote Config, `SoT §11`). These
ambient fields are **not** repeated in each event's param table below.

**Types.** `int` (whole number), `float` (decimal), `string` (enum or id), `bool`. Enum
domains are listed inline.

**Tone.** This file defines *measurement only*. Any user-facing copy referenced here obeys
`SoT §12`: calm, second-person, **no exclamation points**, no confetti.

---

## 1. Session / Retention

GA mapping: `session_start` / `session_end` use GA's native session lifecycle; the rest
are `design:session:*`. Firebase: flat names below.

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `install` | First app open, once per install (after IDFA/anon id minted). | `acquisition_source:string` (`organic`\|`appstore_search`\|`aso`\|`unknown`), `country:string` |
| `session_start` | App foregrounded and a new session window opens. | `session_index:int` (1-based, per user), `day_index:int` (see below), `tiles_owned:int`, `roster_size:int` |
| `session_end` | App backgrounded / session window closes. | `duration_s:int`, `matches_played:int`, `stone_balance:int`, `shard_balance:int` |
| `day_index` | Emitted once per **calendar day boundary** the user is active (drives D1/D7/D30 cohorts). | `day_index:int` (0 = install day, 1 = first return day, …), `is_d1:bool`, `is_d7:bool`, `is_d30:bool` |
| `tutorial_step` | Each tutorial step entered/completed (D1 polish KPI, `SoT §9` D1 mechanism). | `step_id:string`, `status:string` (`enter`\|`complete`\|`skip`), `elapsed_s:int` |
| `tutorial_complete` | Tutorial fully completed — the D1 "pure play" state gate (`SoT §8` D1). | `total_s:int`, `retried_steps:int` |

> `day_index` is the spine of all retention KPIs. It is computed from
> `floor(now_local_midnight − install_local_midnight)` so D1/D7/D30 align with the
> `SoT §9` retention definitions. `is_d1/is_d7/is_d30` are convenience booleans so the
> dashboard can filter without date math.

---

## 2. Economy (Stone + Shards only — `SoT §1`)

Exactly two currencies. `stone_*` events never carry a Shard field and vice-versa. GA
mapping: `economy:<currency>:<flow>:<sink|source>` design events plus GA `resource`
events (sink/source, currency, amount) for the soft-currency flow ledger.

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `tile_claimed` | Player taps a filled tile; **silent** claim completes, Stone enters wallet (`SoT §2` claim flow). | `tile_id:string`, `rank:string` (`T1`\|`T2`\|`T3`), `stone:int` (amount claimed, ≤ rank cap), `fill_pct:float` (0–1 at claim), `was_accelerated:bool` |
| `tile_cap_reached` | A tile newly reaches its pre-claim Stone cap (accrual is clamped at the cap, `SoT §2`). Fires once per fill, feeding the "waited on tile yield" funnel state (`SoT §8` D2). | `tile_id:string`, `rank:string` (`T1`\|`T2`\|`T3`), `cap:int` (the rank cap reached) |
| `stone_earned` | Any Stone credited to wallet (claim, quest, login). | `amount:int`, `source:string` (`tile_claim`\|`daily_quest`\|`login_bonus`), `balance_after:int` |
| `stone_spent` | Any Stone debited from wallet. | `amount:int`, `sink:string` (`unit_unlock`\|`deck_expansion`\|`cosmetic`), `balance_after:int` |
| `unit_unlocked` | A unit transitions to owned via Stone spend (`SoT §2` cost ladder). | `unit_id:string`, `tier:string` (`starter`\|`core`\|`specialist`\|`master`), `role:string` (`vanguard`\|`skirmisher`\|`archer`\|`mage`\|`engineer`\|`champion`), `stone_cost:int`, `roster_size_after:int` |
| `tile_acquired` | A tile becomes owned — **only** from a PvE win (`SoT §2`: no spend grants a tile). | `tile_id:string`, `rank:string` (`T1`\|`T2`\|`T3`), `match_seed:string`, `tiles_owned_after:int` |
| `quest_completed` | Daily quest finished (feeds Stone curve + Plus 2× Shard perk). | `quest_id:string`, `stone_reward:int`, `shard_reward:int`, `plus_multiplier_applied:bool` |
| `deck_expansion_purchased` | Deck slot bought with Stone (`SoT §5`: 3 F2P → up to 6 purchased). | `slots_after:int`, `stone_cost:int` |

> **Guardrail check baked into the schema:** `tile_acquired` has **no** Shard/IAP source
> enum value — a tile can only originate from `match_seed` (a win). `unit_unlocked` has
> **no** Shard cost field — units are Stone-only and never money-gated (`SoT §10.2`).

---

## 3. Combat (`SoT §4`)

GA mapping: `match_start`/`match_end` are `design:combat:*`; `match_end` also drives GA
`progression` (`Start`/`Complete`/`Fail` against the AI-tier ladder, `SoT §4` tiers).

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `match_start` | A PvE match begins (deck locked, board loads). | `match_seed:string`, `ai_tier:int` (1–5, `SoT §4` tiers), `roster_size:int`, `deck_avg_elixir:float` (validator target 2.6–3.0, `SoT §5`), `deck_slot:int`, `is_retry:bool` |
| `match_end` | A PvE match resolves (2/3 towers, or 3:00 damage hold — `SoT §4` win condition). | `match_seed:string`, `result:string` (`win`\|`loss`), `duration_s:int` (≤ 180), `towers_destroyed:int` (0–3), `towers_lost:int` (0–3), `win_by:string` (`towers`\|`damage_timeout`\|`n/a`), `ai_tier:int`, `is_retry:bool` |
| `loss_streak` | Emitted on each loss that extends a consecutive-loss run **on the same match seed** (drives retry-offer eligibility, `SoT §8.2` D15–D21). | `match_seed:string`, `count:int` (consecutive losses on this seed), `ai_tier:int` |
| `difficulty_advanced` | AI tier increases — triggered by **roster expansion, never raw days** (`SoT §4`). | `from_tier:int`, `to_tier:int`, `roster_size:int` |

> `is_retry` lets the dashboard separate first-attempt from retry outcomes — required
> because **rewards are capped at the first attempt** server-side (`SoT §6 Product 3`).
> A retry's `match_end` must reuse the original `match_seed` (identical AI, map, hand).

---

## 4. Monetization (`SoT §6`, `SoT §7`)

GA mapping: `iap_purchase` → GA **business** event (native real-money). All other
monetization beats → `design:monetization:*` so they aggregate without polluting revenue.
**No event in this group fires on app open** (`SoT §10.5`, `SoT §10.6`). Subscription is
**one tier only** (`SoT §6 Product 2`, `SoT §10.8`).

### 4.1 IAP & Shards

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `iap_purchase` | StoreKit 2 purchase **finishes and the Worker validates the receipt** (`source: "server"`, `SoT §7`, `SoT §11`). | `product_id:string`, `usd:float` (localized price normalized to USD), `currency:string` (ISO-4217, e.g. `CAD`\|`AUD`\|`NZD`), `product_type:string` (`shard_pack`\|`accelerator`\|`retry_token`\|`battlepass`\|`tier_skip`\|`plus_sub`), `is_first_purchase:bool` |
| `shard_pack_view` | Shard pack SKU rendered to the player in the Shop tab (always-visible, `SoT §7`). | `product_id:string`, `placement:string` (`shop_tab`\|`d3_starter_banner`), `shard_amount:int`, `usd:float` |
| `shard_pack_purchase` | Shard pack purchase validated (companion to `iap_purchase` with pack context). | `product_id:string`, `shard_amount:int`, `usd:float`, `placement:string` |
| `shard_earned` | Shards credited from a non-IAP source (drip, Plus login bonus, quest 2×). | `amount:int`, `source:string` (`plus_login`\|`daily_quest`\|`free_drop`\|`battlepass_free`), `balance_after:int` |
| `shard_spent` | Shards debited (in-game sink). | `amount:int`, `sink:string` (`accelerator`\|`retry_token`\|`cosmetic`\|`tier_skip`), `balance_after:int` |

> **Accelerator pricing** validated against `SoT §6 Product 1`: `15`/`30`/`60` Shards for
> `T1`/`T2`/`T3`. **Retry pricing**: `20` Shards each / `90` for `5` (`SoT §6 Product 3`).
> **Plus**: `$5.99/month`, one tier (`SoT §6 Product 2`). The dashboard validates emitted
> `usd`/`shards` against these constants and flags drift.

### 4.2 Yield Accelerator (`SoT §6 Product 1`)

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `accelerator_offer_shown` | Accelerate option becomes visible for a tile (tile ≥ 30% filled, not in D1 first-15-min lock — `SoT §6 Product 1` hard caps). | `tile_id:string`, `rank:string` (`T1`\|`T2`\|`T3`), `shards:int` (15\|30\|60), `fill_pct:float`, `placement:string` (`tile_screen`\|`tile_ui_hint`) |
| `accelerator_used` | Player confirms; tile filled to current cap; Shards debited. | `tile_id:string`, `rank:string`, `shards:int`, `stone_granted:int` (= remaining-to-cap, ≤ 1 day yield), `queued_days_after:float` (≤ 3, stacking cap) |

### 4.3 Battle Pass (`SoT §7 Battle Pass v1` — cosmetic-only, 30-day season)

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `battlepass_view` | Pass tab opened or BP reveal banner shown. | `season_id:string`, `placement:string` (`pass_tab`\|`d8_banner`\|`d22_banner`), `current_tier:int`, `track:string` (`free`\|`premium`) |
| `battlepass_purchase` | Premium BP track purchased (validated, cosmetic-only). | `season_id:string`, `usd:float`, `tier_at_purchase:int` |
| `battlepass_tier_unlock` | A tier's reward unlocked by progression (cosmetic). | `season_id:string`, `tier:int`, `track:string`, `reward_id:string`, `reward_kind:string` (`skin`\|`border`\|`emote`) |
| `battlepass_tier_skip` | Tier-skip consumable used (Shard sink or Plus free weekly skip — **no power bundled**, `SoT §7`). | `season_id:string`, `from_tier:int`, `to_tier:int`, `source:string` (`shards`\|`plus_weekly_free`), `shards:int` |

### 4.4 Keepfall Plus (`SoT §6 Product 2` — ONE tier, 7-day trial flag)

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `plus_reveal_shown` | A Plus reveal surface is presented (capped 3 / 30 days, `SoT §8.1`). | `reveal_index:int` (1\|2\|3), `placement:string` (`shop_tab`\|`profile`), `trigger_id:string` (the funnel trigger that fired it) |
| `plus_trial_start` | 7-day free trial begins (only if remote-config trial flag on, `SoT §6 Product 2`, `SoT §11`). | `trial_days:int` (7), `source_reveal_index:int` |
| `plus_subscribe` | Subscription activates (trial→paid or direct), receipt validated by Worker (`source: "server"`). | `is_from_trial:bool`, `usd:float` (5.99), `cohort_day_index:int` (for D14-cohort conversion KPI) |
| `plus_renew` | StoreKit 2 auto-renewal validated by Worker (`source: "server"`). | `renewal_count:int`, `usd:float` |
| `plus_cancel` | Cancellation / lapse detected by Worker. **Cosmetics earned stay owned** (`SoT §6 Product 2`, `SoT §10` trust commitment). | `reason:string` (`user_cancel`\|`billing_lapse`\|`trial_no_convert`), `cosmetics_kept:int`, `was_in_trial:bool` |

> `cosmetics_kept` on `plus_cancel` is the audit signal for the non-negotiable
> keep-cosmetics-on-cancel commitment (`SoT §6 Product 2`; backend has matching tests). It
> must be ≥ the count earned during the sub; the dashboard alarms if any cosmetic is
> revoked.

### 4.5 PvE Retry Tokens (server-authoritative — `SoT §6 Product 3`)

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `retry_offer_shown` | Retry offer surfaced — **only after 3 consecutive losses on the same match**, never first loss (`SoT §8.2` caps). | `match_seed:string`, `loss_count:int` (≥ 3), `placement:string` (`loss_screen`\|`loss_screen_footer`), `trigger_id:string` |
| `retry_token_granted` | A token is sourced (login/Plus/BP/purchase). Server-emitted. | `source:string` (`daily_login`\|`plus_daily`\|`battlepass_free`\|`shard_purchase`), `tokens_after:int`, `cap:int` (3 F2P \| 5 Plus) |
| `retry_token_redeemed` | Worker authorizes a retry and restores one attempt (`source: "server"`). Carries the authority verdict. | `match_seed:string`, `verdict:string` (`granted`\|`denied_retry_of_win`\|`denied_retry_of_retry`), `original_attempt_result:string` (`loss`), `tokens_after:int` |

> `retry_token_redeemed.verdict` is emitted by the **Worker, the authority** (`SoT §6
> Product 3`): it enforces *cannot retry a win*, *cannot retry a retry*, and *rewards
> capped at first attempt*. Denied verdicts are still logged so attempted client
> circumvention is visible on the dashboard.

---

## 5. Funnel (engine instrumentation — `SoT §8`)

These events **bracket** every trigger so each touchpoint's fire→outcome is measurable.
The funnel engine reads **player state, never wall-clock alone** (`SoT §8`); the events
record both the state at fire time and why a trigger was suppressed.

| Event | Fires when | Parameters (type) |
| --- | --- | --- |
| `funnel_trigger_fired` | The engine decides a trigger's precondition + frequency cap pass and presents it. | `trigger_id:string` (see §6 registry), `day_index:int`, `placement:string`, `state_snapshot:string` (compact JSON: e.g. `{"tiles":4,"roster":9,"loss_streak":3}`), `fire_count_30d:int` (this trigger's fires this window) |
| `funnel_trigger_suppressed` | The engine evaluates a trigger and **declines** to show it. | `trigger_id:string`, `day_index:int`, `reason:string` (`precondition_unmet`\|`freq_cap_hit`\|`recently_used_accelerator`\|`already_converted`\|`not_first_loss_rule`\|`post_d30`) |
| `funnel_postd30_suppressed` | A non-converter crosses **D30**; the engine hard-branches off all new triggers (`SoT §8.2` final rule). Emitted once at the boundary. | `day_index:int` (≥ 30), `is_converter:bool` (false for this event by definition), `triggers_now_suppressed:int` |

> `funnel_trigger_fired` for a monetization trigger is followed by the matching §4 event
> (e.g. `plus_reveal_shown`, `accelerator_offer_shown`, `battlepass_view`,
> `retry_offer_shown`) carrying the same `trigger_id`, so conversion per trigger is a join.

---

## 6. 30-Day Funnel → Trigger Registry (`SoT §8`)

The funnel-engine agent consumes these `trigger_id` strings. Each row maps a `SoT §8` table
day to a concrete trigger. **Precondition is player STATE** (the wall-clock day is the
*expected* correlation, not the gate — `SoT §8`). Frequency caps are from `SoT §8.2`.
"Brackets" = the analytics events that wrap the trigger (always
`funnel_trigger_fired` / `funnel_trigger_suppressed`, plus the surface + conversion events).

| `trigger_id` | SoT §8 day | Precondition (player STATE) | Placement | Frequency-cap rule | Bracketing events |
| --- | --- | --- | --- | --- | --- |
| `d2_accelerator_discover` | D2 | Player has **waited on tile yield ≥ once** (a tile reached cap or was viewed while accruing). UI icon only — not an offer. | Tile screen | Once; then governed by accelerator hint cap. Never during D1 first-15-min lock. | `funnel_trigger_fired` → `accelerator_offer_shown` |
| `d3_starter_pack` | D3 | **First unit unlock outside the 6 starters** AND first Stone wall hit (cannot afford next desired unlock). | Shop tab (single banner) | Once. Shard packs otherwise live only in Shop, never auto-presented after D3 (`SoT §7`, `SoT §8.2`). | `funnel_trigger_fired` → `shard_pack_view` → `shard_pack_purchase`/`iap_purchase` |
| `d4_retry_drip` | D4–D6 | Player has **lost ≥ 1 Adept (tier 2) match** AND has 0 retry tokens. | Loss screen footer | Drip = login-granted tokens; offer cosmetic only on a qualifying loss. Not on a first-ever loss with tokens in hand. | `funnel_trigger_fired` → `retry_token_granted` (login) / `retry_offer_shown` |
| `d7_plus_reveal_1` | D7 | Week-1 checkpoint reached AND **unlock pacing has slowed** (time-since-last `unit_unlocked` exceeds the pacing threshold). | Shop tab (single banner) | Plus reveal #1 of max 3 / 30 days (`SoT §8.1`). Not if already Plus. | `funnel_trigger_fired` → `plus_reveal_shown{reveal_index:1}` → `plus_trial_start`/`plus_subscribe` |
| `d8_battlepass_1` | D8–D10 | Player is **engaged & exploring synergies** (≥ N matches across ≥ 2 decks) AND first BP cycle is mid/complete. | Pass tab | Once per cycle surfacing. BP cosmetic-only. | `funnel_trigger_fired` → `battlepass_view{placement:d8_banner}` → `battlepass_purchase` |
| `d11_accel_hint` | D11–D14 | Owns a **T3 tile** AND faces a **specialist Stone wall** (desired specialist unlock 2,500–6,000 Stone unaffordable). | Tile UI | Max **1 per tile per week**; **never** to a player who used an accelerator in the past 7 days (`SoT §8.2`). | `funnel_trigger_fired` → `accelerator_offer_shown{placement:tile_ui_hint}` → `accelerator_used` |
| `d14_plus_reveal_2` | D14 | Two-week checkpoint AND reveal #1 did not convert. **Personalized value framing** (uses player's own roster-time-saved estimate). | Profile | Plus reveal #2 of max 3 / 30 days. Not if already Plus. Respects 3-per-30 cap. | `funnel_trigger_fired` → `plus_reveal_shown{reveal_index:2}` → `plus_trial_start`/`plus_subscribe` |
| `d15_retry_nudge` | D15–D21 | **3 consecutive losses on the same match seed** (`loss_streak.count ≥ 3`), Master tier in sight. | Loss screen | Only after 3 consecutive same-match losses; **never on first loss** (`SoT §8.2`). | `funnel_trigger_fired` → `retry_offer_shown` → `retry_token_redeemed` |
| `d22_battlepass_2` | D22–D28 | Roster **~18–22 / 24** AND second BP cycle available. | Pass tab | Once per second cycle surfacing. | `funnel_trigger_fired` → `battlepass_view{placement:d22_banner}` → `battlepass_purchase` |
| `d22_plus_reveal_3` | D22–D28 | Reveals #1 and #2 both **dismissed/non-converted** AND still not Plus. | Profile | Plus reveal #3 (final) of max 3 / 30 days. Only if #1 and #2 dismissed (`SoT §8` D22–D28). | `funnel_trigger_fired` → `plus_reveal_shown{reveal_index:3}` → `plus_trial_start`/`plus_subscribe` |
| `d29_thanks` | D29–D30 | Month-end checkpoint reached. **No sell.** Free Shard drop + thanks cosmetic (`SoT §8` D29–D30). | Profile | Once. Carries no purchase surface. | `funnel_trigger_fired` → `shard_earned{source:free_drop}` (+ cosmetic grant) |

**Global hard branch.** For any non-converter at `day_index ≥ 30`, the engine emits
`funnel_postd30_suppressed` once and shows **no new triggers**; Plus thereafter is **once
per month** only (`SoT §8.2`). All §6 triggers above are gated behind this branch.

**Never-fire guardrails encoded in the registry (fail the PR if violated, `SoT §10`):**
no trigger fires on app open; no trigger is a "buy or lose forever" power offer; no
discount/FOMO modal; no friend-invite trigger; no second subscription tier; no push
notification sells IAP.

---

## 7. KPI Instrumentation (`SoT §9`)

Each `SoT §9` KPI is derivable from the events above. The dashboard reads these joins;
no KPI depends on a value absent from the source of truth.

> The full **measurability spec** — per-KPI tool (GameAnalytics vs Firebase), the
> conversion-funnel measurability table (each `trigger_id` → precondition → placement →
> frequency cap → fired/suppressed brackets → conversion event), and the post-D30 hard-branch
> observation — lives in [`kpi-dashboard.md`](./kpi-dashboard.md). It mirrors this section and
> the §6 registry; keep the two consistent (change here first, then the dashboard spec).

| KPI (`SoT §9` target) | Source events | Derivation |
| --- | --- | --- |
| **D1 retention** (≥ 40%) | `install`, `day_index{is_d1}` | `count(distinct user with day_index event where is_d1) / count(install)`. Health context: `tutorial_complete`, `tutorial_step` (D1 polish, no D1–D2 monetization friction). |
| **D7 retention** (≥ 18%) | `install`, `day_index{is_d7}` | `count(distinct user @ is_d7) / count(install)`. Context: `tile_claimed`, `unit_unlocked` (economy momentum + roster expansion). |
| **D30 retention** (≥ 8%) | `install`, `day_index{is_d30}` | `count(distinct user @ is_d30) / count(install)`. Context: `plus_subscribe`/`plus_renew`, `battlepass_tier_unlock` (sub value + BP cosmetic pull). |
| **ARPDAU** (≥ $0.25) | `iap_purchase` (server), `session_start`/`day_index` (DAU) | `sum(iap_purchase.usd over day) / count(distinct active user that day)`. Mix skews `battlepass_purchase` + Plus (`plus_subscribe`/`plus_renew`) per `SoT §9`. |
| **ARPPU** (≥ $15) | `iap_purchase` (server) | `sum(usd) / count(distinct user with ≥1 iap_purchase)`. Anchored by `plus_subscribe`/`plus_renew` cohort. |
| **Plus conversion, D14 cohort** (8–12%) | `install`/`day_index` (cohort), `plus_subscribe{cohort_day_index}`, `plus_reveal_shown`, `plus_trial_start` | Numerator: users in the cohort with `plus_subscribe` by `cohort_day_index ≤ 14`. Denominator: cohort installs reaching D14. `plus_reveal_shown{reveal_index}` + `funnel_trigger_fired{d7_plus_reveal_1,d14_plus_reveal_2,d22_plus_reveal_3}` give the two-touchpoint attribution. |
| **Accelerator weekly spenders / DAU** (3–5%) | `accelerator_used`, `session_start`/`day_index` (DAU) | `count(distinct user with ≥1 accelerator_used in trailing 7d) / avg DAU over that week`. `accelerator_offer_shown` gives the show→use funnel for tuning the "light, contextual" target. |

**Day-90 decision matrix (`SoT §9`).** Derived directly from the **D30 retention** and
**ARPDAU** KPIs above; the greenlight gate (Phase 2/3) is **D30 ≥ 8% AND ARPDAU ≥ $0.25**.
No new instrumentation needed — it reads the two KPIs.

**Funnel-health overlays (not §9 targets, but required for dashboard derivability):** per
`trigger_id`, fire rate = `funnel_trigger_fired / (fired + suppressed)`; conversion =
matching surface/purchase event / `funnel_trigger_fired`; suppression breakdown from
`funnel_trigger_suppressed.reason`; post-D30 hygiene from `funnel_postd30_suppressed`.

---

## 8. Guardrail audit hooks (CI can assert these from the schema)

- **Two currencies only:** the union of all `*_earned`/`*_spent` currency surfaces is
  `{stone, shards}` — no third (`SoT §1`, `SoT §10.9`).
- **Tiles from wins only:** `tile_acquired` requires `match_seed`; it has no IAP/Shard
  source (`SoT §2`, `SoT §10.2`).
- **No money-gated units:** `unit_unlocked` carries `stone_cost` only — no Shard/IAP field
  (`SoT §10.2`).
- **One subscription tier:** every `plus_*` event is tierless; there is no `tier`/`plan`
  param on Plus events (`SoT §6 Product 2`, `SoT §10.8`).
- **Cosmetics kept on cancel:** `plus_cancel.cosmetics_kept ≥ earned-during-sub` (`SoT §6
  Product 2`).
- **Retry authority is server:** `retry_token_redeemed` is `source: "server"` with a
  `verdict` enum enforcing the three retry rules (`SoT §6 Product 3`).
- **No app-open selling:** no monetization or funnel event lists `app_open` as a valid
  `placement` (`SoT §10.5`, `SoT §10.6`).
- **Post-D30 hard branch:** non-converter at `day_index ≥ 30` emits
  `funnel_postd30_suppressed` and zero new `funnel_trigger_fired` (`SoT §8.2`).
- **No PvP / no social:** no event in this taxonomy references PvP, friends, or invites
  (`SoT §0`, `SoT §10.10`).
