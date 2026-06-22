# Keepfall — Soft-Launch Submission Checklist

> **Milestone 08 deliverable.** A checkable list for the operator (Chris, CFO) to take the
> Keepfall build through TestFlight and App Store Connect to a phased CA / AU / NZ release.
> Every value traces to [`docs/00-source-of-truth.md`](00-source-of-truth.md) and the IAP
> catalog [`config/iap-catalog.json`](../config/iap-catalog.json). The end-to-end flow and
> the KPI / kill criteria live in [`docs/operator-runbook.md`](operator-runbook.md) — this
> document is the granular submission checklist, the runbook is the operating plan; **they
> cross-reference, they do not restate each other.**
>
> Tone reminder for anything user-facing produced along the way: calm, second-person, **no
> exclamation points** (§12).

---

## A. Build, signing, bundle id

- [ ] Engine: Unity 2023 LTS, iOS Xcode export (source-of-truth §11).
- [ ] Bundle id is exactly `com.vyradata.keepfall` (iap-catalog `_meta.bundleId`).
- [ ] Version `1.0`, build number incremented for each upload.
- [ ] Signed with the Vyra Data Inc. Apple Distribution certificate and an App Store
      provisioning profile for `com.vyradata.keepfall`.
- [ ] Capabilities: In-App Purchase enabled. No capabilities beyond what Phase 1 needs (no
      Push for IAP selling — anti-pattern §10.4; no Game Center / social — §10.10 no social
      graph).
- [ ] Archive validates in Xcode Organizer with no signing or entitlement errors.
- [ ] The Cloudflare Worker is the receipt + retry authority and is reachable from the build's
      configured backend URL (source-of-truth §6, §11; runbook §1 step 1).

---

## B. StoreKit products to create in App Store Connect

Create all six products with the **exact** product ids below (they must match
[`config/iap-catalog.json`](../config/iap-catalog.json), `Keepfall.storekit`,
`IapCatalog.cs`, and the Worker `SHARD_PACKS` map — change one, change all, per
[`docs/storekit-sandbox.md`](storekit-sandbox.md)). USD prices are the Apple default-tier
reference; real storefronts are CA / AU / NZ (section E).

### Consumables (5 Shard packs)

- [ ] `com.vyradata.keepfall.shards.starter` — Starter Shards — USD 0.99 — grants 100 Shards
      (also the D3 single-banner offer)
- [ ] `com.vyradata.keepfall.shards.pouch` — Pouch of Shards — USD 4.99 — grants 550 Shards
- [ ] `com.vyradata.keepfall.shards.chest` — Chest of Shards — USD 9.99 — grants 1,200 Shards
- [ ] `com.vyradata.keepfall.shards.vault` — Vault of Shards — USD 19.99 — grants 2,600 Shards
- [ ] `com.vyradata.keepfall.shards.hoard` — Hoard of Shards — USD 49.99 — grants 7,000 Shards

> The Shard grant is **server-authoritative**: the Worker returns the amount on receipt
> validation and the client credits exactly that (iap-catalog notes; storekit-sandbox). The
> Shard counts above are the reference for QA, not a client-side grant.

### Auto-renewable subscription (Keepfall Plus — one tier)

- [ ] Subscription group: `keepfall_plus` (one group, one product — no second tier,
      anti-pattern §10.8).
- [ ] `com.vyradata.keepfall.plus.monthly` — Keepfall Plus — USD 5.99 — duration 1 month
      (P1M).
- [ ] Introductory offer: **7-day free trial** (P1W, pay-as-you-go free), one per eligible
      account. The trial is also gated behind a Firebase Remote Config flag in-app
      (source-of-truth §6 Product 2; runbook §1 step 5) — confirm the flag is on before ramp.
- [ ] Localized display name "Keepfall Plus" and a calm, exclamation-free description for
      en-CA / en-AU (NZ served by en-AU).
- [ ] All six products are in **Ready to Submit** state and attached to the version, with
      sandbox receipts already validated by the Worker (runbook §1 step 1).

---

## C. App Privacy "nutrition label" (App Store Connect → App Privacy)

Two SDKs collect data: **GameAnalytics** and **Firebase** (source-of-truth §11). Answer the
questionnaire as follows.

- [ ] **Do you collect data from this app?** Yes.
- [ ] **Data types collected:**
  - [ ] **Identifiers** — device identifier (used by GameAnalytics + Firebase to attribute
        sessions/events to a device).
  - [ ] **Usage Data** — product interaction / events (sessions, matches, funnel events,
        purchases for analytics).
  - [ ] **Diagnostics** — crash data and performance (Firebase Crashlytics; runbook §2 +
        §6 crash-free signal).
- [ ] **Purposes:** Analytics, and App Functionality. (No Third-Party Advertising. No
      Developer's Advertising or Marketing of the purchases themselves.)
- [ ] **Linked to the user?** Yes — linked via the device identifier (there is no login /
      email account requirement in Phase 1; identity is the device id used by the analytics
      SDKs and the Worker's cloud-save account).
- [ ] **Used for tracking?** **No.** Keepfall does **not** track users across other companies'
      apps and websites (no third-party ad SDKs, no ad attribution beyond the operator's own
      UA reads). Answer "No" to the tracking question and do **not** present App Tracking
      Transparency for tracking.
- [ ] Privacy Policy URL is set on the listing (store-listing §9) and live before submission.

> Mapping for the form, per data type: Identifiers → Analytics + App Functionality, linked,
> not used for tracking. Usage Data → Analytics + App Functionality, linked, not for tracking.
> Diagnostics → App Functionality (and Analytics), linked, not for tracking.

---

## D. Export compliance

- [ ] **Uses encryption?** Yes — but only **standard HTTPS / TLS** for network calls to the
      Cloudflare Worker (receipts, retries, cloud save). No proprietary or non-standard
      cryptography.
- [ ] Qualifies for the exemption for apps using only standard encryption. Set
      `ITSAppUsesNonExemptEncryption = false` in Info.plist so the per-upload export-compliance
      question is answered automatically.
- [ ] No separate CCATS / export filing required (standard-encryption exemption).

---

## E. Storefront availability and pricing (CA / AU / NZ only)

- [ ] **Availability:** Canada, Australia, New Zealand **only**. Every other territory is
      removed/unchecked (runbook §1 step 4 — no other territories; iap-catalog
      `_meta.storefronts` = CAN, AUS, NZL).
- [ ] **App price:** Free (the app is free; revenue is IAP + subscription).
- [ ] **IAP / subscription pricing:** use Apple **default price tiers**. Select the USD
      reference tier for each product (B) and let Apple populate CA / AU / NZ local prices from
      the tier; do not hand-set per-country prices.
- [ ] Confirm the subscription's 7-day free-trial introductory offer is available in all three
      storefronts.

---

## F. Release strategy

- [ ] **TestFlight internal** first: smoke the economic loop, one sandbox Shard purchase, an
      accelerated sandbox subscription renewal, and one retry-token loss → retry → win;
      confirm the Worker is the authority for receipts and retries (runbook §1 step 2).
- [ ] **TestFlight external (CA / AU / NZ)**: a small external group in the three markets;
      watch crash-free rate and D1 behavior before any public listing (runbook §1 step 3).
- [ ] **Phased App Store release:** enable Apple **phased release** (7-day ramp) so no bad
      build hits 100% at once (runbook §1 step 4).
- [ ] **Manual release after approval:** set the version to release manually (not
      automatically on approval), so the operator controls go-live after the remote-config
      check.
- [ ] **Remote-config check before ramp:** yield rates, accelerator caps, funnel triggers, AI
      thresholds, and the 7-day-trial flag are set in Firebase Remote Config and verified
      before widening — no client rebuild needed to tune any of them (runbook §1 step 5).

---

## G. Review notes (App Store Review → Notes / Sign-in)

Paste calm, factual notes for the reviewer (exclamation-free):

- [ ] **Sandbox / test account:** provide a **StoreKit sandbox Apple Account** so the reviewer
      can exercise a Shard purchase and the Keepfall Plus 7-day-trial subscription end-to-end
      (storekit-sandbox Path C). No separate game login is required — the app opens straight
      into play.
- [ ] **How to reach IAP:** Shard packs live in the Shop tab and are always visible; they are
      never auto-presented after day 3 (source-of-truth §7). Keepfall Plus is on its value
      screen; it is shown at most three times in 30 days and never on app open
      (source-of-truth §8).
- [ ] **No grey-area mechanics, stated plainly:** no loot boxes and no randomized paid rewards
      (Shard packs grant a fixed amount; cosmetics are bought directly). No pay-to-win —
      purchases buy time and cosmetics, never units, tiles, or match outcomes. One subscription
      tier only. Cosmetics earned while subscribed are kept on cancellation. No ads. Phase 1
      is single-player; any PvP hooks are inert placeholders (source-of-truth §0, §6, §10).
- [ ] **Encryption:** standard HTTPS only (section D).

---

## H. Pre-submission guardrail re-check (anti-patterns must all pass)

Confirm none of the shipped build or listing violates the anti-patterns (source-of-truth §10;
[`docs/anti-patterns-guardrails.md`](anti-patterns-guardrails.md); runbook §4):

- [ ] No power sold post-launch; no unit, tile, or rank gated by money.
- [ ] No VIP scaling, no permanent stat boosts, no second subscription tier.
- [ ] No push notifications selling IAP; no FOMO or discount modals on app open.
- [ ] No "buy or lose forever" time-limited power offers; no auto-presented Shard packs after
      D3.
- [ ] Exactly two currencies (Stone + Shards); no third currency; no friend-invite bonuses.
- [ ] **Zero exclamation points** in every shipped string and in all store copy and
      screenshots (§12; design-system §5.5). An exclamation point reaching production is a
      rollback trigger (runbook §6).
- [ ] No PvP / multiplayer shipped; placeholder hooks remain inert and marked (source-of-truth
      §0).

---

## I. Day-90 KPI decision gate (cross-reference, do not restate)

- [ ] Confirm the KPI dashboard is live and feeding from the canonical analytics events before
      ramp (runbook §2; [`docs/analytics-taxonomy.md`](analytics-taxonomy.md)).
- [ ] At day 90, read **D30 retention x ARPDAU** against the decision matrix in
      [`docs/operator-runbook.md`](operator-runbook.md) §3 (the full matrix lives there — do
      not duplicate it here). Greenlight rule for Phase 2/3: **D30 >= 8% AND ARPDAU >= $0.25**,
      both must hold (source-of-truth §9; runbook §3).
- [ ] Hold the rollback / kill criteria from runbook §6 throughout the ramp (crash-free < 99%,
      D1 < 30%, any receipt/retry integrity failure, any guardrail violation in production, or
      a StoreKit / subscription misconfiguration → halt the phased ramp and hotfix).
```
