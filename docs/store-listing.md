# Keepfall — App Store Connect Listing Copy

> **Milestone 08 deliverable (soft launch).** Paste-ready App Store Connect listing copy for
> the CA / AU / NZ soft launch. Every claim traces to
> [`docs/00-source-of-truth.md`](00-source-of-truth.md); the tone follows §12 (calm, honest,
> second-person) and the [`docs/design-system.md`](design-system.md) §5.5 store-copy
> guardrail. Operational flow lives in [`docs/operator-runbook.md`](operator-runbook.md) §1
> (do not duplicate it here). Submission mechanics live in
> [`docs/soft-launch-submission.md`](soft-launch-submission.md).
>
> **Hard rule, absolute on this page:** no exclamation points anywhere — app name, subtitle,
> promo text, description, keywords, what's-new, or review text (§12, design-system §5.5).
> Sell time, never outcomes. Never imply pay-to-win.

---

## 0. Quick reference (field limits)

| ASC field | Limit | Status |
| --- | --- | --- |
| App name | 30 chars | "Keepfall" (8) |
| Subtitle | 30 chars | see §2 |
| Promotional text | 170 chars | see §3 |
| Description | 4000 chars | see §4 |
| Keywords | 100 chars | see §5 |
| What's new | 4000 chars | see §6 |

---

## 1. App name

```
Keepfall
```

Bundle id: `com.vyradata.keepfall`. Developer/seller: Vyra Data Inc.

---

## 2. Subtitle (<= 30 chars)

```
Earn your ground in 5 min
```

(25 characters. Honest framing: territory is earned in combat, matches are short. No power
claim.)

Approved alternates, all <= 30 chars and exclamation-free, if ASO testing prefers a different read:
- `Five-minute strategy battles` (28)
- `Claim tiles, build your deck` (28)
- `Short matches, earned tiles` (27)

---

## 3. Promotional text (<= 170 chars)

```
A calm strategy game with five-minute matches. Win battles to claim tiles, grow your roster, and build decks. You earn your ground here. Nothing is sold for power.
```

(163 characters. Editable post-release without review. Leads with the honest pitch; closes
on the fair-monetization promise.)

---

## 4. Description (full)

Paste verbatim into the ASC Description field.

```
Keepfall is a five-minute strategy game about earned territory.

You take the field, aim your units along a drag-and-release siege arc, and try to bring
down two of three towers before the clock reaches three minutes. Win, and you claim a tile.
That tile yields Stone in real time, even while the app is closed. Stone unlocks more of
your 24-unit roster across six roles: Vanguard, Skirmisher, Archer, Mage, Engineer, and
Champion.

Depth comes from how units combine, not from who paid more. A Mage behind a Vanguard reads
very differently from a Mage in front of one. You build an eight-card deck, draw a five-card
hand, and manage elixir that regenerates one per second. The single-player campaign scales
its AI to the size of your roster, not the calendar, so the challenge tracks your actual
progress.

How we handle money, plainly:

- Tiles come only from winning matches. No purchase ever grants a tile.
- There are exactly two currencies: Stone, which you earn, and Shards, which you can buy.
- Shards buy time and cosmetics. They never buy units, tiles, or power.
- Keepfall Plus is one subscription, 5.99 per month, with a 7-day free trial. It speeds up
  the tiles you already own and adds cosmetics. Same units, same matches, less waiting.
- Cosmetics you earn while subscribed are yours to keep if you cancel.
- The Battle Pass is cosmetic on both tracks, and the free track completes without spending.

What you will not find:

- No pay-to-win. Nothing in the store changes the outcome of a match.
- No second subscription tier, no VIP scaling, no permanent stat boosts.
- No pop-up sales when you open the app. The game opens straight into play.
- No countdown pressure and no buy-or-lose-forever offers.

Keepfall is a Phase 1 single-player experience. You play against the AI, claim ground, and
build a roster at your own pace. Soft-launching in Canada, Australia, and New Zealand.
```

(Approx. 1,690 characters; well under the 4,000 limit. Leads with the honest pitch — earned
territory, five-minute matches, time-not-power monetization — per the §12 tone rule and the
§6 monetization guardrails. Numbers trace to source-of-truth §2, §3, §4, §5, §6, §7.)

---

## 5. Keywords (<= 100 chars, comma-separated)

```
rts,strategy,tower,deck,real time strategy,base,tactics,5 minute,solo,offline,card battle,war
```

(93 characters including commas. Mobile-RTS ASO terms; no spaces after commas to conserve
the 100-char budget. Do not repeat words already in the app name or subtitle — "Keepfall"
and "min" are not duplicated here. No exclamation points, no competitor brand names.)

Notes for the operator iterating ASO (runbook §5, ASO-first):
- "real time strategy" and "card battle" are multi-word phrases Apple indexes as the joined
  string; keep them if conversion holds, swap for single tokens if you need room.
- Localize spelling only where it differs (none of these terms differ across en-CA/en-AU/en-NZ).

---

## 6. What's new (version notes)

For the first soft-launch build (1.0):

```
This is the first Keepfall soft-launch build for Canada, Australia, and New Zealand.

- Single-player campaign: five-minute matches, earned tiles, real-time Stone yield.
- 24-unit roster across six roles, eight-card decks, drag-and-release siege combat.
- Keepfall Plus and the cosmetic Battle Pass, both built to sell time and looks, never power.

Thank you for playing early. Tell us what feels good and what does not.
```

(Exclamation-free. For later builds, keep the same calm voice; state what changed in second
person, no hype.)

---

## 7. Categories

| Field | Value |
| --- | --- |
| Primary category | Games > Strategy |
| Secondary category | Games > Card |

(Strategy is the core read — a five-minute RTS with deck-building. Card is the honest
secondary, given the eight-card deck and five-card hand of source-of-truth §4–§5.)

---

## 8. Age rating answers (App Store Connect questionnaire)

Target rating: **9+** (mild, infrequent stylized fantasy combat; no realism, no gore — the
design system bans gore and aggressive red alarm framing, design-system §5.4).

| Questionnaire item | Answer |
| --- | --- |
| Cartoon or Fantasy Violence | Infrequent/Mild |
| Realistic Violence | None |
| Prolonged Graphic or Sadistic Realistic Violence | None |
| Profanity or Crude Humor | None |
| Mature/Suggestive Themes | None |
| Horror/Fear Themes | None |
| Medical/Treatment Information | None |
| Alcohol, Tobacco, or Drug Use or References | None |
| Simulated Gambling | None |
| Sexual Content or Nudity | None |
| Graphic Sexual Content and Nudity | None |
| Contests | None |
| Unrestricted Web Access | No |
| Gambling | No |
| In-App Purchases present | Yes (5 Shard consumables + Keepfall Plus subscription) |
| Made for Kids | No (not in the Kids category) |

> Note: Keepfall has no loot boxes and no randomized paid rewards, so "Simulated Gambling"
> is None. Shard packs grant a fixed, known amount (source-of-truth §7); cosmetics are bought
> directly, never drawn.

---

## 9. URLs (placeholders — fill before submission)

| ASC field | Value |
| --- | --- |
| Support URL | `https://PLACEHOLDER.vyradata.com/keepfall/support` |
| Marketing URL | `https://PLACEHOLDER.vyradata.com/keepfall` |
| Privacy Policy URL | `https://PLACEHOLDER.vyradata.com/keepfall/privacy` |

> The Privacy Policy URL is required because the app collects data (see
> [`docs/soft-launch-submission.md`](soft-launch-submission.md) App Privacy section). Replace
> all three placeholders with live https URLs before the build is submitted for review.

---

## 10. Localization note

The soft launch ships **English only** across all three storefronts. Apple treats these as
separate locales, but the copy is identical:

- **en-CA (English, Canada)** — same copy as below.
- **en-AU (English, Australia)** — same copy.
- **en-NZ** — App Store Connect has no distinct en-NZ locale; New Zealand uses the **en-AU**
  (or English, default) localization. Set en-AU as the localization that serves the NZ
  storefront.

All three use the same words, the same calm second-person tone, and the same absolute
no-exclamation-point rule. There are no spelling divergences in the chosen copy (the
description and keywords avoid words that differ between Canadian and Australian spelling),
so a single English string set covers all three markets. If a future string needs a
region-specific spelling, branch the locale then — never the tone.
```
