# Keepfall — Anti-Patterns & Guardrails (PR-Failing Checklist)

> This is the **contract** the verification phase and **every PR reviewer** runs. Source:
> [`docs/00-source-of-truth.md`](00-source-of-truth.md) §10 (anti-patterns), §6 (hard
> exclusions), §12 (UI tone). **Any single hit fails the PR.** The grep heuristics are a
> first-pass net, not a substitute for reading the diff — a clean grep does not by itself
> pass an item; a dirty grep always requires a human judgement call.
>
> Run against the diff. Globs assume repo root: `unity/Assets/Scripts/`, `backend/src/`,
> `config/`. Scope: **Phase 1, single-player PvE, iOS.**

---

## A. The 10 anti-patterns (source-of-truth §10)

**A1 — Evolutions-style power sold post-launch.**
Any product that grants combat power for spend.
`grep -rniE '(evolution|evolve|upgrade).*(buy|shard|iap|purchase|price)' unity/Assets/Scripts/`
Detect: any SKU/product whose reward modifies a unit's stats, level, or combat output. Rewards must be cosmetic or time-compression only.

**A2 — Any unit gated by money.**
A unit unlockable/usable only via Shards or IAP.
`grep -rniE '(unit|card|champion|hero).*(shard|iap|purchase|premium|locked.*pay)' unity/Assets/Scripts/`
Detect: any unit-unlock path priced in Shards, or any unit flagged subscriber/premium-only. Units unlock with **Stone only**.

**A3 — VIP scaling or permanent stat boosts.**
Tiered loyalty multipliers or permanent stat increases.
`grep -rniE 'vip|loyaltyTier|permanentBoost|statMultiplier|powerLevel.*\+' unity/Assets/Scripts/`
Detect: any field that permanently raises a stat, or any VIP/tier ladder. The only multiplier allowed is Plus's **+50% yield** (time, not power).

**A4 — Push notifications selling IAP.**
A scheduled/remote notification whose content is a purchase prompt.
`grep -rniE '(push|notification|remoteMessage).*(buy|shard|plus|sale|offer|iap)' unity/Assets/Scripts/`
Detect: any push payload referencing a product, price, sale, or offer. Push may exist for retention only — never selling.

**A5 — FOMO modals on app open.**
A modal shown on launch/app-foreground.
`grep -rniE '(onAppOpen|onLaunch|onResume|appForeground).*(modal|popup|offer|present)' unity/Assets/Scripts/`
Detect: any modal/offer presented from a launch/foreground/resume handler. App open goes straight to play; triggers are state-driven and in-context.

**A6 — Discount modals on app open.**
A launch-time price-cut/limited-time popup.
`grep -rniE '(discount|% ?off|limited.?time|flash.?sale|was.*now).*(modal|popup|banner|present)' unity/Assets/Scripts/`
Detect: any discount/strikethrough-price/limited-time popup, especially from a launch path. No discount modals anywhere on app open.

**A7 — Time-limited "buy or lose forever" power offers.**
A countdown-gated offer that grants power.
`grep -rniE '(countdown|expires|timeLeft|onlyToday|endsIn).*(power|boost|unit|stat|offer)' unity/Assets/Scripts/`
Detect: any offer combining a timer with a power/unit/stat reward. Cosmetic Battle-Pass season FOMO is allowed; **power** FOMO is not.

**A8 — Multi-tier subscriptions.**
More than one subscription product.
`grep -rniE 'subscriptionTier|tier[123]|plusGold|plusPro|premiumPlus' unity/Assets/Scripts/ backend/src/`
Detect: more than one auto-renewable product id, or any tier suffix on the subscription. **Exactly one** tier: Keepfall Plus, $5.99/mo.

**A9 — A third currency beyond Stone + Shards.**
Any earnable/spendable balance that is not Stone or Shards.
`grep -rniE 'enum +Currency|currencyType|gems|gold|coins|crystals|tickets|energy' unity/Assets/Scripts/ backend/src/`
Detect: a Currency enum/type with more than two members, or any new balance field (gems, gold, energy, etc.). **Exactly two currencies, forever.**

**A10 — Friend-invite bonuses.**
Referral/invite rewards (no social graph in Phase 1).
`grep -rniE 'invite|referral|refer.?a.?friend|inviteBonus|socialGraph' unity/Assets/Scripts/ backend/src/`
Detect: any invite/referral code path or reward. There is no social graph in Phase 1.

---

## B. Hard exclusions (source-of-truth §6, §12, §2, §4)

**B1 — No subscriber-only units or tiles.**
`grep -rniE '(plus|subscriber|premium).*(unit|card|tile|rank)|(unit|tile).*subscriberOnly' unity/Assets/Scripts/`
Detect: any unit, tile, or tile rank gated behind Plus. Plus grants time + cosmetics + a deck slot only — never content gating.

**B2 — Cosmetic permanence on cancellation.**
`grep -rniE '(onCancel|onExpire|subscriptionEnded|revoke).*(cosmetic|skin|border)' unity/Assets/Scripts/ backend/src/`
Detect: any code that **removes/revokes** a cosmetic when a subscription cancels or lapses. Cosmetics earned during a subscription are **kept forever**. A passing diff must show (and test) that cancellation leaves cosmetics intact.

**B3 — Server-side retry authority.**
`grep -rniE '(canRetry|retryAllowed|retryReward|retryToken).*=|firstAttempt' unity/Assets/Scripts/`
Detect: any retry **decision** (can-retry, is-win, reward-cap) computed on the **client**. The Cloudflare Worker is the sole authority: cannot retry a win, cannot retry a retry, rewards capped at first-attempt rate. Client may request and render; it must not decide.

**B4 — Two-currency cap.**
`grep -rniE 'enum +Currency' unity/Assets/Scripts/ backend/src/` then assert exactly `Stone` + `Shards`.
Detect: the Currency enum having anything other than exactly Stone and Shards (mirror of A9, applied at the type definition).

**B5 — No exclamation points in UI strings.**
`grep -rnE '"[^"]*!"' unity/Assets/Scripts/ config/`
Detect: any user-facing string containing `!`. Tone is calm, honest, second-person, no shouting, no confetti. (Filter out non-UI literals like log/asserts by hand; every shipped string is exempt of `!`.)

**B6 — Tiles only from combat.**
`grep -rniE '(grantTile|addTile|awardTile|tile.*reward)' unity/Assets/Scripts/ backend/src/`
Detect: any tile-grant call reachable from a purchase, login, quest, accelerator, Plus, or Battle-Pass path. The **only** legitimate caller is the PvE combat-win handler. No spend grants a tile, ever.

**B7 — Difficulty advances on roster, not days.**
`grep -rniE '(difficulty|aiTier).*(daysPlayed|dayCount|wallClock|calendar|elapsedDays)' unity/Assets/Scripts/`
Detect: AI tier / difficulty selection keyed to days-played or wall-clock. The trigger must be **roster size / expansion**; day ranges are expected correlation only, never the input.

---

## C. Scope guardrails (source-of-truth §0)

**C1 — No live PvP / multiplayer code.**
`grep -rniE 'pvp|multiplayer|matchmaking|opponent.*online|netcode|realtimeMatch' unity/Assets/Scripts/ backend/src/`
Detect: any PvP/multiplayer/matchmaking logic that is **not** an inert, clearly-marked placeholder hook. Anything active beyond single-player PvE fails the PR.

**C2 — No third-party scope creep (Android / live-ops).**
`grep -rniE 'android|liveops|live.?ops|google.?play|firebaseMessaging.*offer' unity/Assets/Scripts/ backend/src/`
Detect: Android build paths or live-ops machinery. Phase 1 is **iOS only, no live ops**.

---

## How to use this in review

1. Run the grep heuristics in sections A, B, and C against the PR diff.
2. For every hit, read the surrounding code and decide: real violation → **fail the PR**;
   false positive (e.g. a log line, an inert marked placeholder) → annotate and pass that item.
3. For **B2** (cosmetic permanence) and **B3** (server-side retry authority), a clean grep is
   not enough — confirm the **tests** exist and pass. These two are the highest-trust items.
4. Record the result on the PR. The guardrail review is a **required** approval per
   [`docs/build-sequence.md`](build-sequence.md). No merge to `main` without it.
