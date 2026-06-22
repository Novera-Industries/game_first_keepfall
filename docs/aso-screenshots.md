# Keepfall — App Store Screenshots (ASO Frames)

> **Milestone 08 deliverable (soft launch).** The six App Store screenshot frames for the
> CA / AU / NZ listing. Every caption follows source-of-truth §12 (calm, honest,
> second-person) and the [`docs/design-system.md`](design-system.md) §5.5 store-copy
> guardrail. The underlying art is produced through the Higgsfield pipeline under the
> design-system §5 Higgsfield contract; **all caption and HUD text is composited by us, never
> baked into the art** (design-system §5.4 rule 1, §5.5).
>
> **Hard rule, absolute here:** no exclamation points in any caption (§12, design-system
> §5.4 rule 6 — an exclamation point in a screenshot comp is a rejection condition). Never
> imply a purchase grants power, units, tiles, or wins — only time saved.

---

## 0. Render sizes (every frame)

Produce each of the six frames at all three required display sizes:

| Slot | Device class | Resolution (px) | Role |
| --- | --- | --- | --- |
| Primary | iPhone 6.7" | **1290 x 2796** | The set Apple shows by default; the canonical comp (matches design-system §5.2 asset map) |
| | iPhone 6.5" | **1242 x 2688** | Down-fit from the 6.7" comp |
| | iPhone 5.5" | **1242 x 2208** | Down-fit; check caption legibility at the shorter aspect |

- Orientation: portrait. Color: sRGB. No alpha.
- The painterly scene art comes from the Higgsfield pipeline at source resolution and is
  composited into each device frame; captions and any HUD chrome are layered on top in our
  tooling, not generated into the image (design-system §5.5).
- Re-verify the under-one-second readability test at 5.5" — silhouette, role color, and
  caption must all read at thumbnail size (design-system §5.4 rule 7).

---

## 1. Frame 1 — Match in progress

- **Caption:** `Five-minute matches. Aim your siege arc, take two towers.`
- **Must show:** the painterly battlefield mid-match with live Match HUD (design-system §2.4):
  the 10-segment elixir bar (magenta on surface track), the five-card hand framed in role
  accents, the drag-and-release siege arc as a soft amber-glow dotted parabola toward a tower
  reticle, the 3:00 timer and three enemy tower markers. Combat reads as tactical and calm,
  no alarm-red, no shouting.
- **Traces to:** source-of-truth §4 (combat, siege arc, win condition, 3:00).

## 2. Frame 2 — Tile claim moment

- **Caption:** `Win a match, claim a tile. Stone yields in real time, even when you are away.`
- **Must show:** a ready-to-claim tile card with the stone-gold rim glow (`elevation.glowReady`,
  design-system §2.2 State B) and the silent-claim inline confirmation in calm teal-green:
  `You claimed Tile 07. Stone yield begins now.` (the canonical string, source-of-truth §2 /
  design-system §4.2). The wallet pill shows Stone counting up. No confetti, no modal, no
  burst.
- **Traces to:** source-of-truth §2 (tiles earned only by winning; silent claim; real-time
  accrual that survives app close).

## 3. Frame 3 — Deck builder

- **Caption:** `Build an eight-card deck. Depth is in how units combine, not who paid more.`
- **Must show:** the deck builder (design-system §2.3) with the eight-card slot grid, each
  slot framed in its unit's role primary accent so the six roles read as a spread, the slots
  row (3 / 4 / up to 6), and the calm amber inline validator line, e.g. `Deck ready.` in
  teal-green. Show a roster sampler conveying the 24 units across six roles. No hard-sell
  badges on locked slots.
- **Traces to:** source-of-truth §3 (24 units, 6 roles, synergy) and §5 (8 cards, slots).

## 4. Frame 4 — Battle Pass screen

- **Caption:** `A 30-day Battle Pass. Every reward on both tracks is a cosmetic, free track included.`
- **Must show:** the Battle Pass track row (design-system §2.7) with stacked free and premium
  reward cells, both showing cosmetics only (skins, borders, banners, emotes from the
  "Sunset Watch" Season 1 set, source-of-truth §7). Premium cells indigo-framed, current tier
  marked in magenta. A calm cosmetic note such as `No power, just looks.` No expiry countdown
  animation.
- **Traces to:** source-of-truth §7 (30-day season, cosmetic-only both tracks, free track
  completable F2P).

## 5. Frame 5 — Keepfall Plus value screen

- **Caption:** `Keepfall Plus speeds up the tiles you already own. Same units, same matches, less waiting.`
- **Must show:** the Plus value screen (design-system §2.8): the `Keepfall Plus` display
  headline, the time-not-power subhead (roster completion in about 22 days instead of about
  40), the verbatim perk list (tile yield +50%, +1 deck slot, 2x daily quest Shards, +5
  daily-login Shards, monthly cosmetic drop kept forever, 1 free Battle Pass tier skip per
  week), the prominent trust line `Cosmetics you earn while subscribed are yours to keep if
  you cancel.`, the explicit non-claims (no subscriber-only units, no subscriber-only tiles,
  no combat advantages), and the CTA `Start 7-day free trial` showing one tier at 5.99 per
  month. No urgency timer.
- **Traces to:** source-of-truth §6 Product 2 (one tier, $5.99, 7-day trial, perks, hard
  exclusions, cosmetics kept on cancellation).

## 6. Frame 6 — Post-match silent stat summary

- **Caption:** `A quiet summary after every match. No fanfare, no defeat sting, just the facts.`
- **Must show:** the post-match silent stat summary (design-system §2.5): a plain `Victory`
  or `Match complete` header (no alarm red, no "DEFEAT"), the body stats (towers destroyed,
  damage dealt, match length) and, on a win, the tile claimed and Stone yield in stone-gold,
  with calm `Continue` and `Replay` actions. Convey the canonical line `Match won. You
  claimed a tile. Stone yield begins now.` No victory fanfare, no confetti.
- **Traces to:** source-of-truth §4 (post-match silent summary) and §12 (calm tone).

---

## 7. Caption sheet (copy-paste, exclamation-free)

| # | Frame | Caption |
| --- | --- | --- |
| 1 | Match in progress | Five-minute matches. Aim your siege arc, take two towers. |
| 2 | Tile claim moment | Win a match, claim a tile. Stone yields in real time, even when you are away. |
| 3 | Deck builder | Build an eight-card deck. Depth is in how units combine, not who paid more. |
| 4 | Battle Pass | A 30-day Battle Pass. Every reward on both tracks is a cosmetic, free track included. |
| 5 | Keepfall Plus | Keepfall Plus speeds up the tiles you already own. Same units, same matches, less waiting. |
| 6 | Post-match summary | A quiet summary after every match. No fanfare, no defeat sting, just the facts. |

Every caption above contains zero exclamation points and is written in calm, second-person
voice. The same caption set serves en-CA, en-AU, and en-NZ (NZ served by the en-AU
localization); there are no spelling divergences to localize.

---

## 8. Higgsfield pipeline mapping

These six frames are the `App Store screenshots` row of the design-system §5.2 asset map
(6 frames, 1290 x 2796 source). The freelance illustrator / Higgsfield pipeline delivers the
painterly scene per frame; our compositing tool overlays HUD chrome and the captions above.

Each delivered frame must pass the design-system §5.4 rejection rules, most relevant here:
- **Rule 1 / 4:** no baked text and no baked HUD — elixir bars, wallet pills, buttons, and
  all captions are composited live by us, not painted into the art.
- **Rule 5:** palette stays on the sunset / dusk / brand ramps; crimson appears only as
  Champion identity; each unit's accent matches its role binding (design-system §5.3).
- **Rule 6:** no aggressive red alarm lighting, no shouting/celebratory framing, and **no
  exclamation point** in any comp.
- **Rule 7:** silhouette and role color legible at phone thumbnail size.
- **Rule 8:** no PvP / multiplayer scene, no social imagery, no implied power-for-sale (Phase
  1 is single-player only, source-of-truth §0).
```
