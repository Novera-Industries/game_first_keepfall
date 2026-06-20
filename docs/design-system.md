# Keepfall — UI Design System Spec

> **Deliverable #5.** This document is the contract the freelance illustrator and the
> Higgsfield asset pipeline must satisfy, and the contract the Unity client UI must build
> against. Every token, component, string, and asset rule below traces to
> [`docs/00-source-of-truth.md`](./00-source-of-truth.md) (the canonical constants extracted
> from *Keepfall Master Design Document v1.0*, June 2026).
>
> Scope: **Phase 1 only** — single-player PvE, iOS-first (Unity 2023 LTS + StoreKit 2),
> CA/AU/NZ soft launch. If a value is not in the source of truth, it is not in this spec.
>
> **Companion file:** [`config/design-tokens.json`](../config/design-tokens.json) is the
> machine-readable, copy-pasteable source for every token below.

---

## 0. Brand & visual direction (the one-line read)

Keepfall is **sunset-leaning**: warm oranges, dusk purples, and deep blues, painterly but
**readable at small mobile sizes in under one second**. Soft rim lighting. Low atmospheric
fog. The app chrome reflects the master PDF brand — a **dark plum/indigo background**, a
**magenta-pink accent**, and a **blue (indigo) section accent**.

The tone of the whole product is **calm and honest**. The economy sells *time the player
already earned*, never outcomes. The UI never shouts. There is **no aggressive red alarm
styling** anywhere — defeat is neutral, corrections are a soft amber, and crimson exists
only as the **Champion role** identity color, never as a system error state.

**Hard rule that crosses code and art:** **no exclamation points** appear in any
user-facing string, store screenshot, or store copy. Ever. (Source of truth §12.)

---

## 1. Design tokens

All tokens live in [`config/design-tokens.json`](../config/design-tokens.json). The tables
below are the human-readable mirror. Unity reads the JSON; do not hand-copy hex values into
prefabs — bind to the token keys.

### 1.1 Color — background & surface

| Token | Hex | Use |
| --- | --- | --- |
| `color.bg.base` | `#1A1230` | Dark plum/indigo app background (matches master PDF) |
| `color.bg.deep` | `#120B24` | Deepest plum, behind painterly art + modal scrims |
| `color.bg.raised` | `#241945` | Layered dusk surface, one step up from bg |
| `color.surface.1` | `#2A1E4D` | Default card / panel |
| `color.surface.2` | `#352763` | Elevated surface (sheets, popovers) |
| `color.surface.3` | `#41307A` | Highest surface, selected/active panels |
| `color.surface.overlayScrim` | `rgba(18,11,36,0.72)` | Low-contrast fog scrim behind sheets |

### 1.2 Color — brand & sunset

| Token | Hex | Use |
| --- | --- | --- |
| `color.primary.indigo` | `#5B6BE1` | Blue section accent. Primary interactive color |
| `color.accent.magenta` | `#E2459B` | Magenta-pink brand accent |
| `color.sunset.orange` | `#F2853B` | Warm sunset orange — rim light, warm CTAs |
| `color.sunset.amberGlow` | `#FFB066` | Soft rim-light highlight, low-fog atmosphere |
| `color.sunset.duskPurple` | `#7A4CC2` | Dusk purple mid-tone for gradients + fog |
| `color.sunset.deepBlue` | `#2C3E8C` | Deep blue horizon, grounds the sunset gradient |

### 1.3 Color — currencies (exactly two — source of truth §1)

| Token | Hex | Currency |
| --- | --- | --- |
| `color.currency.stoneGold` | `#E6B450` | **Stone** (soft, earned). Warm gold, never garish |
| `color.currency.shardViolet` | `#A674F0` | **Shards** (premium IAP). Calm violet |

> There is no third currency token and there will never be one. (Anti-pattern §10.9.)

### 1.4 Color — the 6 role accents (source of truth §3)

Each role carries a **primary** and a **secondary** accent, used on unit portraits, deck
slots, and card frames so a player reads a unit's role in under a second.

| Role | Primary | Secondary | Source description |
| --- | --- | --- | --- |
| Vanguard | `#3A5FB0` | `#8A97A6` | Deep blue + steel |
| Skirmisher | `#E0982E` | `#8A6A45` | Amber + leather brown |
| Archer | `#3E8E5A` | `#D8CBB0` | Forest green + bone |
| Mage | `#8E5BD4` | `#F2853B` | Violet + ember orange |
| Engineer | `#6B7480` | `#C77B45` | Slate grey + copper |
| Champion | `#C0392B` | `#E6B450` | Crimson + gold |

> Champion crimson (`#C0392B`) is a **unit identity color only**. It is never used for
> system errors, alarms, or "buy now" pressure.

### 1.5 Color — state (no aggressive red alarm — explicit guardrail)

| Token | Hex | Use |
| --- | --- | --- |
| `color.state.success` | `#4FB286` | Calm teal-green: valid deck, claim complete |
| `color.state.info` | `#5B6BE1` | Informational (reuses brand indigo) |
| `color.state.caution` | `#E0982E` | Soft amber for gentle correction (deck validator). **Not red. Not an alarm.** |

> There is intentionally **no red error/alarm token**. Validation corrections are amber and
> phrased in second person. A lost match is neutral, never punishing red.

### 1.6 Color — text

| Token | Hex | Use |
| --- | --- | --- |
| `color.text.primary` | `#F4F1FA` | Primary text (warm off-white) |
| `color.text.secondary` | `#C4BBDC` | Supporting text |
| `color.text.tertiary` | `#8E84AE` | Captions, timestamps, disabled |
| `color.text.onAccent` | `#1A1230` | Dark text on gold/amber bright fills |
| `color.text.onPrimary` | `#FFFFFF` | Text on indigo/magenta fills |

### 1.7 Spacing, radius, elevation

| Spacing | px | | Radius | px | | Elevation | shadow |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `1` | 4 | | `sm` | 6 | | `1` | `0 1px 2px rgba(18,11,36,.40)` |
| `2` | 8 | | `md` | 10 | | `2` | `0 4px 12px rgba(18,11,36,.45)` |
| `3` | 12 | | `lg` | 16 | | `3` | `0 8px 24px rgba(18,11,36,.50)` |
| `4` | 16 | | `xl` | 24 | | `glowReady` | `0 0 20px rgba(230,180,80,.30)` |
| `6` | 24 | | `pill` | 999 | | `glowAccent` | `0 0 16px rgba(226,69,155,.35)` |
| `8` | 32 | | | | | | |

> 4pt base scale. **Touch targets never below 44pt** (`spacing.touchTargetMin`). `glowReady`
> and `glowAccent` are reserved for the ready-to-claim tile only — a slow ambient pulse,
> never a flash.

### 1.8 Type scale & families

- **UI / humanist sans:** `Inter` (fallback `system-ui, -apple-system, SF Pro Text`).
- **Display weight for titles:** `Fraunces 600` (fallback `SF Pro Display, Georgia`) —
  painterly, editorial; pairs with the sunset key art.
- **Numerals:** `Inter` with **tabular numerals** (`tnum`) for currency, elixir, and timers,
  so values do not jitter as they change.

| Token | Size (pt) | Weight | Family | Use |
| --- | --- | --- | --- | --- |
| `displayXL` | 34 | 600 | display | App title, Plus headline |
| `displayL` | 28 | 600 | display | Screen headers |
| `titleL` | 22 | 600 | ui | |
| `titleM` | 18 | 600 | ui | Card titles, section heads |
| `body` | 16 | 400 | ui | Default body (min sustained reading) |
| `bodyStrong` | 16 | 600 | ui | |
| `label` | 14 | 500 | ui | Buttons, chips, wallet pill |
| `caption` | 12 | 500 | ui | Timestamps, validator hints |
| `numXL` | 28 | 700 | numeric | Big yield / Stone totals |
| `numM` | 16 | 700 | numeric | Wallet values, elixir count |

### 1.9 Motion tokens

| Token | Value | Use |
| --- | --- | --- |
| `duration.instant` | 80ms | Tap feedback |
| `duration.fast` | 140ms | Hover, small state change |
| `duration.base` | 220ms | Standard transition |
| `duration.slow` | 320ms | Sheet / screen transition |
| `duration.ambient` | 2400ms | Tile-fill shimmer, ready-glow pulse (loop) |
| `easing.standard` | `cubic-bezier(.4,0,.2,1)` | Default |
| `easing.decelerate` | `cubic-bezier(0,0,.2,1)` | Entrances |
| `easing.gentle` | `cubic-bezier(.33,0,.2,1)` | Calm, soft moves |

> **No spring/overshoot easing is defined, on purpose.** Overshoot reads as a celebratory
> bounce, which is banned for purchases and claims. When the OS requests reduced motion,
> disable all ambient loops and replace transitions with an opacity cross-fade at
> `duration.fast`.

### 1.10 design-tokens.json (copy-pasteable)

The full block is written to [`config/design-tokens.json`](../config/design-tokens.json).
Use that file as the single source; do not fork the values here. Excerpt:

```json
{
  "color": {
    "bg":       { "base": "#1A1230", "deep": "#120B24", "raised": "#241945" },
    "primary":  { "indigo": "#5B6BE1" },
    "accent":   { "magenta": "#E2459B" },
    "currency": { "stoneGold": "#E6B450", "shardViolet": "#A674F0" },
    "role": {
      "vanguard":   { "value": "#3A5FB0", "accentSecondary": "#8A97A6" },
      "skirmisher": { "value": "#E0982E", "accentSecondary": "#8A6A45" },
      "archer":     { "value": "#3E8E5A", "accentSecondary": "#D8CBB0" },
      "mage":       { "value": "#8E5BD4", "accentSecondary": "#F2853B" },
      "engineer":   { "value": "#6B7480", "accentSecondary": "#C77B45" },
      "champion":   { "value": "#C0392B", "accentSecondary": "#E6B450" }
    },
    "state":    { "success": "#4FB286", "caution": "#E0982E" }
  }
}
```

---

## 2. Component specs

All components sit on `color.bg.base`. Default card surface is `color.surface.1`, radius
`lg`, elevation `2`. Text uses the type scale in §1.8. No component uses a red alarm fill.

### 2.1 Currency wallet pill

A single, persistent, top-aligned pill showing **both** currencies — never a third slot.

- Container: `radius.pill`, `color.surface.2`, `elevation.1`, height 36, horizontal padding
  `spacing.3`.
- Two segments separated by a hairline `color.border.subtle`:
  - **Stone:** stone-gold coin glyph + `numM` value in `color.currency.stoneGold`.
  - **Shards:** violet shard glyph + `numM` value in `color.currency.shardViolet`.
- A small `+` affordance on the Shards segment opens the Shop tab (it does **not** open a
  pop-up purchase modal — Shard packs live in Shop, source of truth §7).
- **Update animation:** value count-up over `duration.base`, `easing.gentle`. No coin-burst,
  no confetti.

### 2.2 Tile card — T1 / T2 / T3, three states

Tiles are earned **only by winning combat** (source of truth §2); the card never offers a
"buy a tile" path. Rank is shown as a small `T1` / `T2` / `T3` badge tinted from the sunset
ramp (T1 deepBlue → T2 duskPurple → T3 orange) so rank reads instantly.

Yield constants shown on the card trace to §2:

| Rank | Yield | Cap | Fill time |
| --- | --- | --- | --- |
| T1 | 10 Stone/hr | 120 | 12h |
| T2 | 25 Stone/hr | 300 | 12h |
| T3 | 60 Stone/hr | 720 | 12h |

**State A — Filling**
- Painterly tile environment art fills the card; a **stone-gold progress meter** (radial or
  bottom-bar) shows fraction of cap, with the live Stone count in `numM` and a "fills in
  Hh Mm" caption.
- Ambient shimmer loop at `duration.ambient`. Calm, low amplitude.

**State B — Ready to claim**
- Meter reaches cap → card gains the `elevation.glowReady` stone-gold rim glow, pulsing
  slowly at `duration.ambient`. **No badge shouting, no exclamation.**
- Caption switches to: `Ready to claim. <cap> Stone.`

**State C — Claimed**
- After the **silent claim interaction** (below), the meter resets to empty, glow removes,
  and the card returns to Filling with a quiet caption update.

**Silent claim interaction (source of truth §2):**
1. Player taps a tile in state B.
2. Stone enters the wallet (wallet pill count-up, `duration.base`).
3. A brief inline confirmation appears in `color.state.success`:
   `You claimed Tile NN. Stone yield begins now.`
4. **No confetti. No modal. No sound sting. No celebratory bounce.** The tile silently
   re-enters Filling. This is the canonical example string from the source of truth.

> The **Yield Accelerator** affordance only appears on a tile that is **≥ 30% filled**, is
> **locked during the first 15 minutes of D1 play**, and obeys the funnel frequency caps
> (§2.10 below + source of truth §6/§8). It is a small inline icon on the tile, never a
> pop-up.

### 2.3 Deck builder — slot + inline validator

- **Slots row:** shows the player's available deck slots — **3 (F2P) · 4 (Plus) · up to 6
  (purchased expansion)** (source of truth §5). Locked slots beyond the player's entitlement
  show a calm "Plus" or "Expansion" hint chip, never a hard-sell badge.
- **Card slot:** square, `radius.md`, framed in the unit's role primary accent (§1.4) so the
  8-card composition reads as a spread of roles at a glance.
- **Inline validator messaging** (calm, second-person, amber `color.state.caution` — never
  red), enforcing source-of-truth §5 deck rules:

  | Rule | Inline message |
  | --- | --- |
  | Exactly 8 cards | `Add N more to reach 8 cards.` |
  | Avg elixir 2.6–3.0 | `Average elixir is X.X. Aim for 2.6 to 3.0.` |
  | At least one Vanguard | `Add a Vanguard to hold your line.` |
  | At least one Champion | `Add a Champion as your win condition.` |
  | Valid | `Deck ready.` (in `color.state.success`) |

  Messages appear inline beneath the slots, not as a blocking modal. The "Save" action is
  disabled (not hidden) until the deck is valid, with the reason shown in text.

### 2.4 Match HUD

Lives over the painterly battlefield. Minimal, readable, never obscuring the play area.

- **Elixir bar (0–10):** a 10-segment bottom bar, fill in `color.accent.magenta` over a
  `color.surface.1` track. Regenerates **1 per second, cap 10** (source of truth §4). Current
  value in `numM` tabular numerals so it does not jitter. No flashing when full — a steady
  glow only.
- **5-card hand:** the 5 drawn cards (from the 8-card deck) along the bottom, each framed in
  its role accent, each showing its elixir cost as a `numM` badge. The next-card preview sits
  to the left. Affordable cards are full-opacity; unaffordable cards dim to 55% — no red lock.
- **Siege-arc aim affordance:** on drag from a hand card, render a **drag-and-release siege
  arc** (source of truth §4) — a soft dotted parabola in `color.sunset.amberGlow` from the
  card to the aim point, with a target reticle on a valid tower. Release to deploy. The arc
  uses `easing.gentle`; it is an aiming aid, not a flourish.
- **Tower / timer readout:** 3 enemy towers shown as small markers; the **3:00 match timer**
  in `numM`. Win condition reminder (destroy 2 of 3 towers, or hold most damage at 3:00) is
  available in a quiet info affordance, never a banner.

### 2.5 Post-match silent stat summary

After the match (source of truth §4): a calm, full-screen **silent** summary. No victory
fanfare, no defeat sting.

- Header: outcome stated plainly — `Victory` or `Match complete` (no exclamation, no
  "DEFEAT" in alarm red; loss header reads `Match complete` in `color.text.primary`).
- Body: towers destroyed, damage dealt, match length, and — on a win — the **tile claimed**
  and **Stone yield** that begins, in `color.currency.stoneGold`.
- Actions: `Continue` (primary indigo) and an optional `Replay` (secondary).
- On a **loss**, the **retry-token loss-screen footer** (§2.9) may appear, server-gated.

### 2.6 Shop card

- Card: `color.surface.1`, `radius.lg`, role/cosmetic art thumbnail on top.
- Title (`titleM`), one-line calm description, price as a Shard or Stone chip.
- **Shard packs are always visible in the Shop tab and never auto-presented after D3**
  (source of truth §7). The D3 first offer is the **$0.99 Shard starter pack** shown as a
  single in-tab card, not a modal.
- Cosmetic shop runs a **14-day rotation, 3–5 SKUs** — a small "rotates in Nd" caption in
  `color.text.tertiary`, stated as information, **not** a countdown-pressure animation.
- Buy button: primary; on successful purchase, a quiet inline confirmation only. **No
  celebratory bounce, no confetti** on purchase.

### 2.7 Battle Pass track row — free vs premium

Source of truth §7: **30-day season, cosmetic-only on both tracks, free track completable
F2P.**

- A horizontal tier row; each tier is a node with two stacked reward cells:
  - **Free track** (top): cosmetic reward, claimable by all.
  - **Premium track** (bottom): cosmetic reward, indigo-framed.
- Both reward cells show **cosmetics only** — never power, never a stat boost (anti-pattern
  §10.1/§10.3).
- A **tier-skip** chip uses the tier-skip consumable; it carries a clear note that it skips
  tiers and **does not bundle power** (source of truth §7). Keepfall Plus grants
  **1 free tier skip / week** (source of truth §6) — surfaced here as an available skip, not
  a sale.
- Current tier marker uses `color.accent.magenta`. Progress is shown calmly; no
  "expires in" pressure animation.

### 2.8 Keepfall Plus value screen

One subscription tier only — **$5.99/month, 7-day free trial** (trial gated behind a
remote-config flag) (source of truth §6).

- Headline in `displayXL`, calm and honest, e.g. `Keepfall Plus`.
- Subhead frames the value as **time, not power**: roster completion in ~22 days instead of
  ~40.
- **Perk list (verbatim from source of truth §6):**
  - Tile yield rate **+50%** on all owned tiles
  - **+1 deck slot** (4 total)
  - **2× daily quest Shard** rewards
  - Daily login bonus **+5 Shards**
  - Monthly cosmetic drop (skin or border) — **kept forever**
  - **1 free Battle Pass tier skip / week**
- **Trust line, prominent and required:**
  `Cosmetics you earn while subscribed are yours to keep if you cancel.`
- **Explicit non-claims shown as reassurance, not fine print:** no subscriber-only units, no
  subscriber-only tiles, no combat advantages (source of truth §6 hard exclusions).
- CTA: `Start 7-day free trial` or `Subscribe — $5.99/month`. No urgency timer. This screen
  is **shown max 3 times in 30 days** by the funnel engine (source of truth §8), never on app
  open.

### 2.9 Retry-token loss-screen footer

Server-authoritative (the Cloudflare Worker is the authority — source of truth §6 Product 3).

- A footer on the post-match loss summary. Appears **only after 3 consecutive losses on the
  same match**, never on a first loss (source of truth §8 frequency caps).
- Content: explains a token restores **one attempt** with **identical AI, map seed, and
  starting hand** — and that **the player still has to win**.
- Token sources are shown plainly (daily login, Plus, Battle Pass, or Shard purchase:
  **20 Shards each / 90 Shards for 5**, source of truth §6).
- **Client never decides eligibility.** The footer's "Use retry" action calls the Worker; the
  client renders whatever the Worker authorizes. The server enforces: **cannot retry a win,
  cannot retry a retry, rewards capped at the first-attempt rate.** The UI must not imply a
  retry can beat those rules.
- Copy is calm and non-coercive (see §3 strings). No "buy or lose forever" framing
  (anti-pattern §10.7).

### 2.10 Funnel banner (single, dismissible, never modal-on-open)

The one sanctioned promotional surface. Driven by the trigger engine, which **reads player
state, never wall-clock alone** (source of truth §8).

- A single inline banner anchored to the relevant screen (Shop, Tile UI, Loss screen,
  Profile, or Pass tab — per the §8 funnel map). Never floats over app open.
- Always **dismissible** with a visible close affordance; dismissal is respected by the
  frequency caps.
- `color.surface.2`, `radius.lg`, one line of calm copy + one action. No countdown, no
  pulsing urgency, no scrim behind it.
- Respects every §8 cap: Plus max 3×/30 days then 1×/month; accelerator hints max 1 per tile
  per week and never to someone who used one in the last 7 days; **after D30 a non-converter
  sees no new triggers** (hard branch).

---

## 3. Motion rules

Motion is **calm, short, and purposeful**. It clarifies state; it never celebrates a
purchase or pressures a decision.

**DO**
- Use `duration.base` (220ms) for standard transitions, `duration.slow` (320ms) for
  screen/sheet changes, `easing.gentle`/`easing.standard`.
- Count currency up/down over `duration.base` with tabular numerals.
- Loop the tile-fill shimmer and ready-to-claim glow slowly at `duration.ambient` (2400ms),
  low amplitude.
- Respect OS reduced-motion: drop ambient loops, cross-fade only.

**DON'T (these mirror the anti-patterns — a violation fails the PR)**
- **No confetti**, anywhere — not on claim, win, purchase, or tier-up.
- **No celebratory bounce / overshoot** on purchase or subscribe. (No spring easing exists in
  the tokens for this reason.)
- **The claim interaction is silent** — no burst, no sting, no modal.
- **No FOMO modal on app open.** No discount modal on app open. (Anti-patterns §10.5/§10.6.)
- **No countdown-pressure animation** — rotation/expiry timers are static informational text,
  never animated urgency.
- **No flashing red alarm** state on losses, errors, or validation.
- **No push-notification-driven** animations selling IAP (anti-pattern §10.4).

---

## 4. UI copy

### 4.1 Tone rules (source of truth §12)

- **Calm, honest, second-person.** No shouting. No confetti language.
- **No exclamation points. Ever.** In any string, anywhere — including store screenshots and
  store copy.
- Sell **time**, never outcomes. Never imply a purchase wins a match or grants power.
- Corrections are gentle and actionable, not punitive.
- Sentence case. Plain words over hype. State facts; let the player decide.

### 4.2 Canonical strings

| Context | String |
| --- | --- |
| Tile claim (canonical example) | `You claimed Tile 07. Stone yield begins now.` |
| Tile ready to claim | `Ready to claim. 120 Stone.` |
| Unit unlock | `You unlocked Longshot. Add them to a deck when you are ready.` |
| Deck valid | `Deck ready.` |
| Deck needs a Vanguard | `Add a Vanguard to hold your line.` |
| Deck needs a Champion | `Add a Champion as your win condition.` |
| Accelerator offer (contextual) | `This tile is filling. You can fill it to its cap now with Shards.` |
| Accelerator confirm | `Tile filled to its cap. Stone is in your wallet.` |
| Plus reveal | `Keepfall Plus speeds up the tiles you already own. Same units, same matches, less waiting.` |
| Plus trust line | `Cosmetics you earn while subscribed are yours to keep if you cancel.` |
| Retry offer (after 3 losses, server-gated) | `A retry restores this match with the same AI, map, and hand. You still have to win it.` |
| Retry unavailable (cannot retry a win/retry) | `This match cannot be retried.` |
| Post-match summary (win) | `Match won. You claimed a tile. Stone yield begins now.` |
| Post-match summary (loss) | `Match complete. Adjust your deck and try again when you are ready.` |
| Shop starter pack (D3, single banner) | `A small Shard pack to get you started. It lives in the Shop whenever you want it.` |
| Battle Pass (cosmetic note) | `Every reward on both tracks is a cosmetic. No power, just looks.` |

> Every string above contains **zero exclamation points** and is written in calm,
> second-person voice. Localization for CA/AU/NZ uses en-CA / en-AU spelling but keeps the
> same tone and the same no-exclamation rule.

---

## 5. Higgsfield contract

This section is the **meeting point between the code design system and the art pipeline**. It
restates the master visual direction and binds every generated asset to a spec the freelance
illustrator and the Higgsfield pipeline must satisfy. **Screenshots and store copy carry no
exclamation points** — this is a rejection condition, not a preference.

### 5.1 Master visual direction (restated for the pipeline)

- **Sunset-leaning palette:** warm oranges (`#F2853B`), dusk purples (`#7A4CC2`), deep blues
  (`#2C3E8C`), grounded on the dark plum/indigo brand background (`#1A1230`).
- **Painterly but readable at small mobile sizes** — silhouette and role color must read in
  **under one second** on a phone.
- **Soft rim lighting. Low atmospheric fog.** Warm key light, cool shadow.
- **Brand accents:** magenta-pink (`#E2459B`) and indigo (`#5B6BE1`) appear in chrome and
  framing, not painted into the world art.
- Every unit portrait is keyed to its **role color accent** (§1.4) so the roster reads as six
  visually distinct families.

### 5.2 Asset map

| Asset | Count | Resolution | Notes |
| --- | --- | --- | --- |
| Key art | 1 (+ variants) | **2732×2048** | Hero sunset scene, painterly, no text baked in |
| Unit portraits | **24** (6 starter + 10 core + 6 specialist + 2 master) | **2048×2048** | Each keyed to its role accent (§1.4); 1 portrait per unit |
| Tile environments — T1 | set | 2048×2048 | Early-rank tile world, deep-blue-leaning sunset |
| Tile environments — T2 | set | 2048×2048 | Mid-rank, dusk-purple-leaning |
| Tile environments — T3 | set | 2048×2048 | Late Phase 1, warm-orange-leaning |
| Battle Pass cosmetics | per season (30-day, cosmetic-only) | 2048×2048 | Skins / borders; both free + premium tracks |
| Shop cosmetics | 3–5 per 14-day rotation | 2048×2048 | Cosmetic SKUs only — no power items |
| Loading art | set | 2732×2048 | Painterly, calm; no progress-pressure framing |
| App Store screenshots | **6** | **1290×2796** | iPhone 6.7"; HUD/text composited by us, not baked |

> Counts trace to source of truth §3 (24 units / 6 roles), §7 (shop + pass rotation), and the
> Phase 1 store-listing target (CA/AU/NZ). Portraits map to roles as: each role has 1 starter
> + 1 specialist; the 10 core are distributed across roles; the 2 master units are lateral
> (cross-role) options — render them as sideways choices, **not** bigger/stronger silhouettes
> (no power-creep visual language).

### 5.3 Role-to-accent binding for the 24 portraits

| Role | Portrait accent (primary / secondary) | Sample named units (source of truth §3) |
| --- | --- | --- |
| Vanguard | `#3A5FB0` / `#8A97A6` (deep blue + steel) | Bulwark, Standardbearer |
| Skirmisher | `#E0982E` / `#8A6A45` (amber + leather brown) | Hound, Pathfinder |
| Archer | `#3E8E5A` / `#D8CBB0` (forest green + bone) | Longshot, Volley |
| Mage | `#8E5BD4` / `#F2853B` (violet + ember orange) | Spark, Wildfire |
| Engineer | `#6B7480` / `#C77B45` (slate grey + copper) | Tower, Snare |
| Champion | `#C0392B` / `#E6B450` (crimson + gold) | Captain, Berserker |

### 5.4 Rejection rules (any one rejects the asset)

A generated asset is **rejected and regenerated** if it shows any of:

1. **Visible text** of any kind baked into the art (letters, numbers, runes-as-letters,
   signage). All copy is composited by us.
2. **Glyph artifacts** — garbled pseudo-text, AI lettering smears, watermark-like marks.
3. **Anatomy errors** — extra/missing fingers or limbs, broken hands, melted faces, impossible
   joints.
4. **HUD overlays** baked into the art — no fake elixir bars, health bars, buttons, or
   currency pills painted in. The HUD is rendered live by Unity.
5. **Palette violations** — colors outside the sunset/dusk/brand ramps, neon clashes, a unit
   whose accent does not match its role binding (§5.3), or use of crimson as anything but
   Champion identity.
6. **Tone violations** — aggressive red alarm lighting, gore, shouting/celebratory framing,
   or any **exclamation point** in a screenshot or store-copy comp.
7. **Readability failure** — silhouette or role color not legible at phone thumbnail size
   (the under-one-second test).
8. **Scope violations** — any PvP/multiplayer scene, social/friend-invite imagery, or implied
   power-for-sale (anti-patterns §10).

### 5.5 Store-listing copy guardrail (binds to §4)

App Store screenshots, captions, and the store description for CA/AU/NZ:
- Use the canonical calm tone (§4.1) and **contain zero exclamation points**.
- Never claim a purchase grants power, units, tiles, or wins — only time saved.
- Caption text is composited by us over the 1290×2796 art; the underlying art carries no text
  (§5.4 rule 1).

---

## 6. Traceability

Every section above maps to the source of truth:

| This spec | Source of truth |
| --- | --- |
| §1.3 two currencies | §1 Currencies |
| §1.4 / §5.3 role accents | §3 Roster |
| §2.2 tile yields & silent claim | §2 Tile economy |
| §2.3 deck rules & slots | §5 Deck-building rules |
| §2.4 elixir / hand / siege arc / win condition | §4 Combat |
| §2.6 shop, §2.7 Battle Pass | §7 Shop + Battle Pass |
| §2.8 Plus value, §2.9 retry tokens | §6 Monetization |
| §2.10 funnel banner & caps | §8 Conversion funnel |
| §3 motion DON'Ts, §5.4 rejection rules | §10 Anti-patterns |
| §4 copy, §5.5 store copy | §12 UI copy tone |
| §5 Higgsfield contract scope | §0 Scope guardrail |
