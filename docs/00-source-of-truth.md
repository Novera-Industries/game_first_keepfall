# Keepfall — Source of Truth (Canonical Constants)

> Extracted verbatim from **Keepfall Master Design Document v1.0** (June 2026) and the
> Phase 1 build prompt. Every number, rule, and string in the Unity client, the
> Cloudflare Workers backend, the remote-config schema, the funnel engine, and the
> test suite **must trace back to this file**. If a value is not here, it is not canonical
> — add it here first, then implement it.
>
> Owner: Chris Wood, CFO · Vyra Data Inc. · Scope: **Phase 1 only** (single-player PvE, iOS).

---

## 0. Scope guardrail (operating-plan slide 34)

> *No live-ops or multiplayer Keepfall. Anything beyond a single-player Studio Demo kills
> timeline and budget.*

Phase 1 is **single-player PvE only**. No PvP. No live ops. No Android. No multiplayer
infrastructure of any kind. Any request that expands beyond this is rejected and the PR
fails. Placeholder PvP hooks may exist in code but must be inert and clearly marked.

---

## 1. Currencies (exactly two — a third currency is an anti-pattern)

| Currency | Type | How obtained | Notes |
| --- | --- | --- | --- |
| **Stone** | Soft (earned) | Tile yield, quests, claims | Funds unit unlocks, deck expansion, minor cosmetics |
| **Shards** | Premium (IAP) | StoreKit 2 purchase, drips, Plus | Funds accelerator, retry tokens, cosmetics, tier skips |

There is **no third currency**. Ever.

---

## 2. Tile economy

### Yield by tile rank
| Rank | Yield rate | Cap before claim | Time to fill cap |
| --- | --- | --- | --- |
| T1 (early) | **10** Stone/hour | **120** Stone | 12 hours |
| T2 (mid) | **25** Stone/hour | **300** Stone | 12 hours |
| T3 (late Phase 1) | **60** Stone/hour | **720** Stone | 12 hours |

- Accrual is **real-time** and **survives app close** (computed from wall-clock delta on
  resume; clamped to the 12-hour cap).
- Tiles are earned **only by winning a PvE match**. No spend grants a tile. Ever.
- **Claim flow:** tap a filled tile → **silent** claim (no confetti, no modal) → Stone
  enters wallet. Example copy: `"You claimed Tile 07. Stone yield begins now."`

### F2P daily Stone curve (design target, used to validate tuning)
| Window | Stone/day | Tiles owned | Progression |
| --- | --- | --- | --- |
| Day 1–3 | 200–400 | 3–5 | starter units unlocking |
| Day 4–14 | 800–1,500 | 6–9 | core units unlocking |
| Day 15–30 | 2,000–4,000 | 9–12 | specialist tier in reach |

Roster completion: **35–45 days F2P · 22–25 days with Keepfall Plus.**

### Unit unlock cost ladder (Stone)
| Unit tier | Count | Stone cost | Expected unlock day (F2P) |
| --- | --- | --- | --- |
| Starter | 6 | Free / 50–150 | Day 1–3 |
| Core | 10 | 300–1,200 | Day 4–14 |
| Specialist | 6 | 2,500–6,000 | Day 12–25 |
| Master | 2 | 10,000–15,000 | Day 25–40 |
| **Total** | **24** | | |

---

## 3. Roster (24 units, 6 roles)

**Authoritative count:** 24 total = **6 starter + 10 core + 6 specialist + 2 master**
(from the unlock cost ladder in the master design, which supersedes the "4 per role"
phrasing in the build prompt's Higgsfield table).

Distribution reconciliation: each role has **1 starter** and **1 specialist** (6 + 6).
The **10 core** are distributed across roles. The **2 master** units are lateral options
(cross-role) — they offer sideways choices, **never strict upgrades** (this prevents the
Clash Royale L12–L16 ladder where new cards obsolete old ones).

| Role | Function | Color accent (art) | Named sample units |
| --- | --- | --- | --- |
| Vanguard | Tank, frontline damage absorption | Deep blue + steel | Bulwark, Standardbearer |
| Skirmisher | Mobility, flanking, harassment | Amber + leather brown | Hound, Pathfinder |
| Archer | Ranged single-target damage | Forest green + bone | Longshot, Volley |
| Mage | Area-of-effect, splash, control | Violet + ember orange | Spark, Wildfire |
| Engineer | Buildings, traps, defensive control | Slate grey + copper | Tower, Snare |
| Champion | Heavy hitter, win-condition unit | Crimson + gold | Captain, Berserker |

**Synergy is the depth engine.** Power comes from how units combine, not individual stats
(a Mage behind a Vanguard >> a Mage in front of one). The synergy load is distributed
across many pairings so **no single unit can be made the gate**. No unit is ever gated by
money.

---

## 4. Combat

- **5-card hand** drawn from an **8-card deck**.
- **Elixir** regenerates at **1 per second**, cap **10**.
- **Drag-and-release siege arc** — players aim units at tower targets.
- **Win condition:** destroy **2 of 3** enemy towers, **or** hold most damage at the
  **3:00** mark.
- **Post-match:** silent stat summary, claimed tile, Stone yield, optional replay.

### AI difficulty tiers (5)
| Tier | Name | Behavior | Player progression |
| --- | --- | --- | --- |
| 1 | Apprentice | Plays reactively, weak synergies | D1–D3 |
| 2 | Adept | Mixed offense/defense, predictable cycles | D4–D7 |
| 3 | Tactician | Strong cycle management, baits well | D8–D14 |
| 4 | Commander | Reads player decks, deck-specific counters | D15–D25 |
| 5 | Marshal | Punishes mistakes, near-optimal play | D25+ |

- **Difficulty advances on roster expansion, never raw days played.** (The day ranges are
  the *expected* correlation, not the trigger.)
- **Loss-rate targets (unassisted F2P):** ≤ 25% Apprentice · ~40% Tactician · ~55% Marshal.

---

## 5. Deck-building rules (enforced in UI + validator)

- Exactly **8 cards**.
- Target **average elixir cost 2.6–3.0**.
- At least **one Vanguard** and **one Champion** required.
- **Deck slots:** 3 (F2P) · 4 (Plus) · up to 6 (purchased expansion).

---

## 6. Monetization — three products (each compresses time, none sells outcomes)

> *Convenience compresses time the player already earned. Power buys outcomes they didn't.
> Every product compresses time. None sells outcomes.*

### Product 1 — Yield Accelerator (consumable, Shard-priced)
Fills **one tile to its current cap** instantly.
- **Hard caps (enforced in code):**
  - ≤ **1 day of yield per purchase** (never more than one full cap fill).
  - **No stacking past 3 days** of queued yield per tile.
  - **Locked during the first 15 minutes of D1** play.
  - Tile must be **≥ 30% filled** before the accelerate option appears.
- **Pricing:** **15 / 30 / 60 Shards** for **T1 / T2 / T3** (cheaper per Stone at higher tiers).

### Product 2 — Keepfall Plus (subscription — ONE tier, $5.99/month)
StoreKit 2 auto-renewable. **7-day free trial** (recommended; gated behind a remote-config flag).

| Perk | Value |
| --- | --- |
| Tile yield rate **+50%** on all owned tiles | Roster completion ~22 days instead of ~40 |
| **+1 deck slot** (4 total) | More loadout flexibility |
| **2× daily quest Shard** rewards | +100 Shards/week |
| Daily login bonus **+5 Shards** | +150 Shards/month |
| Monthly cosmetic drop (skin or border) | Exclusive to active month, **kept forever** |
| **1 free Battle Pass tier skip / week** | Completionist convenience |

**Hard exclusions (enforced in code + tests):**
- No subscriber-only **units**.
- No subscriber-only **tiles** or tile ranks.
- No **PvP** perks (PvP arrives at Phase 2 — placeholder hooks only, inert).
- No **combat advantages** of any kind.
- **Cosmetics earned during a subscription are kept on cancellation** — non-negotiable
  trust commitment. **Write tests for this.**

### Product 3 — PvE Retry Tokens (server-authoritative)
After a PvE loss, a token restores **one attempt** with **identical AI, map seed, and
starting hand**. The player still has to win.
- **Sources:** daily login **1/day (cap 3)** · Keepfall Plus **+1/day (cap 5)** · Battle Pass
  free track **~5/season** · Shard purchase **20 Shards each** or **90 Shards for 5**.
- **Rules enforced server-side (authority is the Worker, not the client):**
  - **Cannot retry a win.**
  - **Cannot retry a retry.**
  - **Rewards capped at the first-attempt rate.**

---

## 7. Shop + Battle Pass

### Cosmetic shop
- **14-day rotation**, **3–5 SKUs** per rotation.
- Shard IAP packs wired through **StoreKit 2 sandbox**; receipts validated on Cloudflare Workers.
- Shard packs are **always visible in the Shop tab**, **never auto-presented after D3**.
- D3 first offer: **$0.99 Shard starter pack** (single banner) — this is the `starter` tier below.

### Shard IAP pack ladder (StoreKit 2 consumables)

Five repeatable consumable packs. **Effective $/Shard improves with size** (honest bulk
convenience — buying more compresses cost, never outcomes). Bundle id prefix
`com.vyradata.keepfall.shards.*`. USD reference prices (Apple default tiers); real storefronts
are CA/AU/NZ. Shards are premium currency only — they buy convenience and cosmetics, never units,
tiles, or power.

| Tier | StoreKit product id | USD | Shards | $/100 Shards |
| --- | --- | --- | --- | --- |
| starter | `com.vyradata.keepfall.shards.starter` | 0.99 | 100 | $0.99 |
| pouch | `com.vyradata.keepfall.shards.pouch` | 4.99 | 550 | $0.91 |
| chest | `com.vyradata.keepfall.shards.chest` | 9.99 | 1,200 | $0.83 |
| vault | `com.vyradata.keepfall.shards.vault` | 19.99 | 2,600 | $0.77 |
| hoard | `com.vyradata.keepfall.shards.hoard` | 49.99 | 7,000 | $0.71 |

- `starter` doubles as the D3 single-banner offer (§8). All five are otherwise always-available
  in the Shop tab and never auto-presented after D3.
- The Keepfall Plus subscription (§6 Product 2) is StoreKit product
  `com.vyradata.keepfall.plus.monthly` — **one tier**, $5.99/month, 7-day free trial.
- The product→Shards mapping is **server-authoritative**: the Worker validates the receipt and
  returns the Shard grant for the product; the client credits the server's amount.

### Battle Pass v1
- **30-day season.** **Cosmetic-only** rewards on **both** free and premium tracks.
- **Free track completable F2P.**
- **Tier-skip consumable** available — **do not bundle power** with it.

### Battle Pass Season 1 — "Sunset Watch"

30 tiers over 30 days. **12 cosmetics = 5 free + 7 premium** (within the Part B Higgsfield
8–12 SKU brief). Reaching tier 30 completes the free track with no spend. Canonical data:
[`config/battlepass-season1.json`](../config/battlepass-season1.json); the Unity client mirrors
it in `BattlePassSeason1`. `type` + `displayName` are the art-pipeline contract.

| Tier | Track | Cosmetic | Type | Id |
| --- | --- | --- | --- | --- |
| 1 | Premium | Sunset Standardbearer | banner | `cosmetic.s1.banner.sunset_standardbearer` |
| 3 | Free | First Light | border | `cosmetic.s1.border.first_light` |
| 6 | Premium | Ember Mage | skin | `cosmetic.s1.skin.ember_mage` |
| 8 | Free | Dusk Banner | banner | `cosmetic.s1.banner.dusk` |
| 12 | Premium | Dusk Vanguard | skin | `cosmetic.s1.skin.dusk_vanguard` |
| 15 | Free | Quiet Victory | emote | `cosmetic.s1.emote.quiet_victory` |
| 18 | Premium | Riverkeep | banner | `cosmetic.s1.banner.riverkeep` |
| 22 | Free | Wayfarer | border | `cosmetic.s1.border.wayfarer` |
| 24 | Premium | Gilded Hoard | border | `cosmetic.s1.border.gilded_hoard` |
| 28 | Premium | Marshal's Salute | emote | `cosmetic.s1.emote.marshals_salute` |
| 30 | Free | Lone Tile (capstone) | tile | `cosmetic.s1.tile.lone_tile` |
| 30 | Premium | Golden Hour Fortress (capstone) | tile | `cosmetic.s1.tile.golden_hour_fortress` |

Every entry is a cosmetic — no unit, currency, tile-as-power, or stat. The premium track is a
cosmetic purchase; owning it never confers a combat advantage.

---

## 8. 30-day conversion funnel

> Each trigger appears **only after the player has felt the friction the product solves**,
> never before. No modals, no "limited time" pressure, no push notifications about IAP.
> The **trigger engine reads player state, never wall-clock alone.**

| Day | Player state | Trigger | Where |
| --- | --- | --- | --- |
| D1 | Tutorial done. 3 tiles, 6 starters. | **None — pure play** | — |
| D2 | Waited for tile yield ≥ once | Accelerator made discoverable (UI icon only) | Tile screen |
| D3 | First unit unlock outside starters. First Stone wall. | $0.99 Shard starter pack (single banner) | Shop tab |
| D4–D6 | Roster expanding. May have lost first Adept match. | Retry tokens via daily login drip | Loss screen footer |
| D7 | Week-1 checkpoint. Unlock pacing slowing. | Keepfall Plus first reveal (single banner) | Shop tab |
| D8–D10 | Engaged, exploring synergies. | Battle Pass first cycle complete or mid-cycle | Pass tab |
| D11–D14 | Mid-pacing wall. Specialist 2,500–6,000 Stone. | Yield accelerator usage hint near T3 tile | Tile UI |
| D14 | Two-week retention checkpoint. | Plus reveal #2 — personalized value framing | Profile |
| D15–D21 | Master tier in sight. More difficulty walls. | Retry token nudge after 3 consecutive losses | Loss screen |
| D22–D28 | Roster ~18–22/24. Late Phase 1 engagement. | Second Battle Pass cycle. Plus reveal #3 if dismissed | Pass + Profile |
| D29–D30 | Month-end retention checkpoint. | **No new triggers.** Free Shard drop + thanks cosmetic. | Profile |

### Frequency caps (enforced in trigger engine)
- **Keepfall Plus** shown **max 3 times / 30 days**. After D30, **once per month**.
- **Yield accelerator hints** max **1 per tile per week**. Never to a player who used one
  in the past 7 days.
- **Retry token offers** only after **3 consecutive losses on the same match**. Never on
  first loss.
- Shard packs **always in Shop**, never auto-presented after D3.
- **After D30 a non-converter sees NO new triggers** — focus shifts to retention. **Code
  this as a hard branch.**

---

## 9. Phase 1 KPI targets (instrument + dashboard)

| Metric | Target | Mechanism |
| --- | --- | --- |
| D1 retention | ≥ 40% | Tutorial polish · no monetization friction D1–D2 |
| D7 retention | ≥ 18% | Tile economy momentum · roster expansion |
| D30 retention | ≥ 8% | Subscription value · Battle Pass cosmetic FOMO |
| ARPDAU | ≥ $0.25 | Mostly Battle Pass + Plus · light Shards spend |
| ARPPU | ≥ $15 | Plus subscribers anchor this number |
| Plus conversion (D14 cohort) | 8–12% | Two reveal touchpoints + concrete value framing |
| Accelerator weekly spenders / DAU | 3–5% | Light, contextual prompts only |

### Day-90 decision matrix (operator runbook)
| D30 retention | ARPDAU | Decision |
| --- | --- | --- |
| ≥ 15% | ≥ $0.40 | Phase 2 (async PvP) + Phase 3 prep |
| 8–14% | $0.25–$0.39 | Phase 2 only — re-evaluate Phase 3 after global |
| < 8% | any | Pause. Refactor core loop or wind down. |
| any | < $0.25 | Pause. Refactor monetization presentation, do not add PvP. |

Phase 2/3 greenlight (build-prompt pillar): **D30 ≥ 8% AND ARPDAU ≥ $0.25.**

---

## 10. Anti-patterns — fail the PR if any appear

1. Evolutions-style power sold post-launch.
2. Any unit gated by money.
3. VIP scaling or permanent stat boosts.
4. Push notifications selling IAP.
5. FOMO modals on app open.
6. Discount modals on app open.
7. Time-limited "must buy or lose forever" power offers.
8. Multi-tier subscriptions.
9. A third currency beyond Stone + Shards.
10. Friend-invite bonuses (no social graph in Phase 1).

---

## 11. Stack

| Layer | Choice |
| --- | --- |
| Engine | Unity 2023 LTS |
| Platform | iOS only (StoreKit 2) |
| Backend | Cloudflare Workers + D1 (accounts + cloud save + receipt/retry authority only) |
| Analytics | GameAnalytics + Firebase |
| Remote config | Firebase Remote Config (tune yield rates + accelerator caps + funnel triggers + AI thresholds without rebuild) |

---

## 12. UI copy tone (all strings)

Silent post-match stat summaries. **No shouting. No confetti. No exclamation points in UI
strings.** Honest, calm, second-person.

> Example: `"You claimed Tile 07. Stone yield begins now."`

---

## 13. 13-week build sequence (milestone branches)

| Weeks | Milestone branch | Deliverable |
| --- | --- | --- |
| 1–3 | `milestone/01-economy` | Editor-playable economic loop end-to-end |
| 3–5 | `milestone/02-shop-iap` | Purchase flow validated in StoreKit 2 sandbox |
| 5–7 | `milestone/03-accelerator` | Tile interaction loop complete |
| 6–8 | `milestone/04-battlepass` | First 30-day season content ready |
| 8–10 | `milestone/05-plus` | Subscription renewal + cosmetic drop flow tested |
| 9–11 | `milestone/06-retry-tokens` | Difficulty curve locked, retry plumbing wired |
| 10–12 | `milestone/07-funnel-analytics` | Conversion model measurable |
| 12–13 | `milestone/08-soft-launch` | TestFlight build, CA/AU/NZ App Store listing, ASO |
