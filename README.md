# Keepfall — Phase 1 (Single-Player PvE)

A 5-minute mobile real-time strategy game built on a tile-resource economy. **Phase 1 is
single-player PvE only**, iOS-first (Unity 2023 LTS + StoreKit 2), shipping to Canada,
Australia, and New Zealand. No PvP. No live ops. No Android. No multiplayer infrastructure.

> Owner: Chris Wood, CFO · Vyra Data Inc. · Source of truth: **Keepfall Master Design
> Document v1.0**. Every constant, rule, and UI string in this repo traces to
> [`docs/00-source-of-truth.md`](docs/00-source-of-truth.md) — read it first.

## Repo layout

| Path | What |
| --- | --- |
| [`docs/`](docs/) | Source of truth, design system, analytics taxonomy, runbook, build sequence, anti-pattern guardrails, roster |
| [`config/`](config/) | Firebase Remote Config schema + defaults, design tokens |
| [`unity/`](unity/) | Unity 2023 LTS client — one runtime assembly `Keepfall.Game`, EditMode tests under `Assets/Tests/EditMode` |
| [`backend/`](backend/) | Cloudflare Workers (TypeScript) + D1 — accounts, cloud save, StoreKit 2 receipts, **retry-token authority** |

## Deliverable map (build prompt → repo)

1. **Unity project skeleton + milestone branches** → [`unity/`](unity/) · branches `milestone/01-economy` … `milestone/08-soft-launch` (see [`docs/build-sequence.md`](docs/build-sequence.md))
2. **Cloudflare Workers backend** → [`backend/`](backend/) (accounts, cloud save, receipt validation, retry-token authority)
3. **Remote config schema** → [`config/remote-config.schema.json`](config/remote-config.schema.json) + [`config/remote-config.defaults.json`](config/remote-config.defaults.json)
4. **Funnel trigger engine + analytics taxonomy** → [`unity/Assets/Scripts/Funnel/`](unity/Assets/Scripts/Funnel) + [`docs/analytics-taxonomy.md`](docs/analytics-taxonomy.md)
5. **UI design system spec** → [`docs/design-system.md`](docs/design-system.md) + [`config/design-tokens.json`](config/design-tokens.json) (the contract for the Higgsfield art pipeline)
6. **Test suite** → backend Vitest (`backend/test/`) + Unity EditMode (`unity/Assets/Tests/EditMode/`)
7. **Operator runbook** → [`docs/operator-runbook.md`](docs/operator-runbook.md)

## The five required tests

| Requirement | Where |
| --- | --- |
| Tile yield accrual across app restart | `unity/Assets/Tests/EditMode/TileAccrualRestartTests.cs` |
| Accelerator caps | `unity/Assets/Tests/EditMode/AcceleratorCapTests.cs` |
| Retry-token server authority | `backend/test/retry.test.ts`, `backend/test/retry.routes.test.ts` |
| Subscription cosmetic permanence | `unity/Assets/Tests/EditMode/SubscriptionCosmeticPermanenceTests.cs` |
| Deck validator | `unity/Assets/Tests/EditMode/DeckValidatorTests.cs` |

## Running the backend

```bash
cd backend
npm install
npm run dev          # wrangler dev → http://127.0.0.1:8787  (GET /v1/health)
npm test             # Vitest — 53 tests (accounts, saves, receipts, retry authority)
npm run typecheck    # tsc --noEmit
```

A `.claude/launch.json` is wired so the backend can be started by name (`keepfall-api`, port 8787).

> ⚠️ **Path caveat:** `vitest-pool-workers` (workerd) cannot resolve modules when the project
> path contains spaces (this repo lives under an iCloud folder named `KEEPFALL GAME`). `wrangler
> dev` works in place, but **`npm test` must be run from a space-free path** — copy `backend/`
> to e.g. `/tmp/keepfall-backend` and run there, or relocate the project out of the spaced path.
> Typecheck and `wrangler dev` are unaffected.

## Opening the Unity client

Open `unity/` in Unity 2023 LTS (2023.2.x). All runtime gameplay code is in the single
`Keepfall.Game` assembly; run EditMode tests via the Test Runner. Pure logic (tile accrual,
wallet, deck/accelerator validators, funnel engine) is free of `UnityEngine` so it is unit-testable.

**Milestone 01 — editor-playable economy loop:** in the editor menu choose **Keepfall ▸ Economy
▸ Create Economy Loop Scene** (generates `Assets/Scenes/EconomyLoop.unity` via Unity's own
serializer), or **Spawn Economy Loop Demo** (`Ctrl/Cmd+Shift+E`) to drop the bootstrap into the
open scene. Press Play and watch the Console: the loop seeds a save, grants a tile from a "win",
accrues Stone against the wall clock, and exposes silent-claim / unit-unlock actions on the
`EconomyLoopDriver` component's right-click context menu. (Editor on-ramp lives in
`unity/Assets/Editor/EconomyDemoMenu.cs`; the loop itself is `Assets/Scripts/Economy/`.)

**Milestone 02 — Shop + IAP:** **Keepfall ▸ Shop ▸ Log Current Rotation** / **Simulate Starter
Pack Purchase** exercise the cosmetic rotation and the Shard-pack purchase→validate→credit flow in
editor. StoreKit 2 sandbox testing uses `unity/StoreKitConfig/Keepfall.storekit` — see
[docs/storekit-sandbox.md](docs/storekit-sandbox.md).

**Milestone 03 — tile interaction loop:** **Keepfall ▸ Tile ▸ Run Tile Interaction Loop**
(`unity/Assets/Editor/TileInteractionDemo.cs`) prints the complete accrue → accelerate → claim
loop to the Console (no Play mode needed); `TileInteractionLoopTests` verifies it in CI.

**Milestone 04 — Battle Pass Season 1:** **Keepfall ▸ Battle Pass** logs the "Sunset Watch"
track (30 tiers, 12 cosmetics = 5 free + 7 premium) and simulates free-track completion / premium
unlock. Canonical content: [config/battlepass-season1.json](config/battlepass-season1.json);
`BattlePassSeason1Tests` pins it.

**Milestone 05 — Keepfall Plus:** **Keepfall ▸ Plus** logs the perks + monthly-drop schedule and
simulates subscribe → 3 monthly drops → cancel (cosmetics kept). One tier, $5.99/mo, 7-day trial;
12-month drop schedule in [config/plus-monthly-drops.json](config/plus-monthly-drops.json);
`PlusMonthlyDropFlowTests` + `SubscriptionCosmeticPermanenceTests` pin the renewal/permanence flow.

**Milestone 06 — Retry Tokens + difficulty:** **Keepfall ▸ Combat** logs the AI difficulty curve
(tier by roster size, not days) and simulates a server-authoritative retry after 3 losses. The
curve is pinned by `DifficultyCurveTests`; retry authority lives in the Worker (`retry.test.ts`,
`retry.routes.test.ts`) with `RetryTokenClient` deferring to it (`RetryTokenClientTests`).

## Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on push/PR:
- **backend** — `npm ci` + `tsc --noEmit` + the full Vitest suite on a clean checkout (no spaces → the path caveat above does not bite in CI).
- **unity-tests** — the EditMode suite via [game-ci](https://game.ci). **Opt-in:** set repo variable `UNITY_TESTS_ENABLED=true` and add secrets `UNITY_LICENSE` (or `UNITY_SERIAL`) + `UNITY_EMAIL` + `UNITY_PASSWORD`. Until then the job is skipped (neutral), so CI stays green on the backend alone. This is how the EditMode tests + editor scripts get compiled and run, since no Unity runner exists locally.

Remote: `origin` → https://github.com/Novera-Industries/game_first_keepfall.git

## Non-negotiables (enforced + audited)

Sell time, never outcomes · exactly two currencies (Stone + Shards) · tiles only from combat ·
no paywalled units · no permanent stat boosts · one subscription tier · cosmetics kept on
cancellation · retry rules enforced server-side · no FOMO/discount modals on open · no
exclamation points in UI copy. See [`docs/anti-patterns-guardrails.md`](docs/anti-patterns-guardrails.md).

## Build status

Scaffold built and verified: anti-pattern guardrail audit **passed** (0 violations); backend
suite **53/53 passing**; remote-config keys reconciled to a single canonical contract across
client, schema, and bundled defaults; client/server retry contract aligned end-to-end.
Unity EditMode tests are authored but require a Unity install to execute (no runner in CI yet).
