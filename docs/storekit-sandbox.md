# Keepfall — StoreKit 2 Sandbox Runbook

> A short, calm guide to exercising the Shard purchase flow for `milestone/02-shop-iap`.
> Everything here traces to [`docs/00-source-of-truth.md`](00-source-of-truth.md) §6, §7.
> Scope: **Phase 1, single-player PvE, iOS only.** Shards buy convenience and cosmetics —
> never units, tiles, or power.

The purchase loop is the same in every environment:

```
StoreKit 2 purchase  →  signed transaction (JWS)
        →  POST /v1/receipts/validate  (the Cloudflare Worker is the authority)
        →  Worker returns { valid, shardsGranted, alreadyProcessed }
        →  client credits the SERVER's Shard amount, only when valid and not already processed
```

The product→Shards mapping is **server-authoritative**. The client never grants premium
currency on its own; it credits exactly what the Worker returns (§7).

---

## Products

The canonical catalog is authored once and mirrored across the codebase. If you change a
number, change all of these together:

- [`config/iap-catalog.json`](../config/iap-catalog.json) — machine-readable catalog
- [`unity/StoreKitConfig/Keepfall.storekit`](../unity/StoreKitConfig/Keepfall.storekit) — StoreKit 2 sandbox config
- `unity/Assets/Scripts/Monetization/IapCatalog.cs` — client mirror + fallback
- `backend/src/lib/config.ts` → `SHARD_PACKS` — server-authoritative grant map

### Shard packs (consumables)

| Product id | USD | Shards | $/100 Shards |
| --- | --- | --- | --- |
| `com.vyradata.keepfall.shards.starter` | 0.99 | 100 | $0.99 |
| `com.vyradata.keepfall.shards.pouch` | 4.99 | 550 | $0.91 |
| `com.vyradata.keepfall.shards.chest` | 9.99 | 1,200 | $0.83 |
| `com.vyradata.keepfall.shards.vault` | 19.99 | 2,600 | $0.77 |
| `com.vyradata.keepfall.shards.hoard` | 49.99 | 7,000 | $0.71 |

Effective $/Shard improves with size — honest bulk convenience, never an outcome. The
`starter` pack doubles as the D3 single-banner offer; all five are otherwise always
available in the Shop tab and never auto-presented after D3 (§7, §8).

### Subscription

`com.vyradata.keepfall.plus.monthly` — Keepfall Plus, the one tier, $5.99/month, 7-day
free trial. Bundle id: `com.vyradata.keepfall`. (Subscription receipt handling is covered
in milestone 05; this runbook focuses on the Shard packs.)

---

## Path A — Editor simulation (no device, no App Store)

The fastest way to see the loop. It fabricates a StoreKit-2-shaped JWS and validates it
against an editor-only fake Worker that returns the catalog Shard grant.

1. Open the Unity project.
2. **Keepfall ▸ Shop ▸ Log Current Rotation** — prints the 14-day cosmetic rotation and the
   always-available Shard packs from the canonical catalog.
3. **Keepfall ▸ Shop ▸ Simulate Starter Pack Purchase** — runs
   `SandboxStoreKitPurchaser` → `ShopService.BuyShardPackAsync` against the fake backend, then
   logs the new wallet balance. You should see 100 Shards credited.

The editor on-ramp lives in `unity/Assets/Editor/ShopDemoMenu.cs` and
`unity/Assets/Editor/EditorFakeBackendClient.cs` (both editor-only).

To exercise the same logic against the **real** Worker locally, point an
`IBackendClient` at a `wrangler dev` instance with `ENVIRONMENT` set to anything other than
`production`. In non-production the Worker accepts a well-formed sandbox JWS whose payload
JSON carries `bundleId`, `productId`, `transactionId`, and `type` — exactly what
`SandboxStoreKitPurchaser` fabricates.

---

## Path B — Xcode StoreKit configuration testing

When you have a built Xcode project (after a Unity iOS export):

1. In Xcode, open **Product ▸ Scheme ▸ Edit Scheme… ▸ Run ▸ Options**.
2. Set **StoreKit Configuration** to `Keepfall.storekit`
   (from `unity/StoreKitConfig/`). This runs purchases against the local StoreKit config —
   no App Store Connect round trip, no real charges.
3. Run on a simulator or device. Buying a pack returns a real StoreKit 2
   `Transaction.jwsRepresentation`, which the client forwards to the Worker.
4. Keep the Worker's `ENVIRONMENT` non-production until the Apple Root CA - G3 chain check is
   enabled (see the production gate in `backend/src/lib/receipts.ts`).

---

## Path C — On-device sandbox (App Store Connect sandbox tester)

For a true end-to-end check before TestFlight:

1. Create the five consumables and the Plus subscription in App Store Connect with the exact
   product ids above.
2. Sign in to a **sandbox Apple Account** on the device (Settings ▸ App Store ▸ Sandbox
   Account).
3. The on-device purchaser is `NativeStoreKitPurchaser` — the StoreKit 2 bridge stub. Off
   device or in the editor it returns `native_storekit_unavailable` and **never fabricates a
   transaction**; the native call is wired on a Mac with Xcode as part of milestone 02/08.
4. A real sandbox purchase produces a verified `Transaction`; the client sends its
   `jwsRepresentation` to `POST /v1/receipts/validate`, and Shards land in the wallet after
   the Worker confirms the grant.

---

## Idempotency and trust

- A re-validated `transactionId` returns `alreadyProcessed: true`. The response still reports
  `shardsGranted`, but the client credits **nothing** on a replay — the player is never
  double-credited and never charged without credit.
- Finish a StoreKit transaction only **after** the Worker confirms the credit.
- No purchase grants units, tiles, or power. No celebratory confetti or bounce on purchase,
  and no exclamation points in any string (§12).
