# Keepfall API — Phase 1 backend

Cloudflare Workers + D1. **Scope (source-of-truth §11): accounts + cloud save +
StoreKit 2 receipt validation + the retry-token authority ONLY.** No gameplay
simulation. Single-player PvE. iOS-only.

> The Worker is the **source of truth for retry rules**. The client is never
> trusted. (source-of-truth §6 Product 3.)

---

## Endpoints

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| `GET`  | `/v1/health` | none | Liveness. |
| `POST` | `/v1/accounts` | none | Upsert by Apple user id. Returns `{ accountId, token, expiresInSeconds }`. |
| `POST` | `/v1/save` | bearer | Push cloud save blob. Last-write-wins by `updatedUtc` + device. |
| `GET`  | `/v1/save` | bearer | Pull latest cloud save. |
| `POST` | `/v1/receipts/validate` | bearer | Verify a StoreKit 2 JWS. Records receipt (idempotent by `transactionId`); sets entitlement for Plus / non-consumables. |
| `POST` | `/v1/matches/start` | bearer | Record a PvE attempt (`matchSeed` + `aiTier`), `result=pending`. |
| `POST` | `/v1/matches/result` | bearer | Record `win`/`loss` + reward. Retry rewards clamped to the first-attempt cap. |
| `POST` | `/v1/retry/request` | bearer | **Eligibility check (read-only).** Runs the same authority gate as redeem (`canRedeem`) without spending a token or creating an attempt. Returns `{ eligible, reason, tokenBalance }`. |
| `POST` | `/v1/retry/redeem` | bearer | **The authority.** Validates cannot-retry-a-win / cannot-retry-a-retry, decrements a token atomically, creates the retry attempt with **identical seed + AI tier**, stamps reward cap = first-attempt reward. |
| `POST` | `/v1/retry/grant-daily` | bearer | Idempotent daily grant: +1 base (cap 3), +1 headroom if Plus active (cap 5). Refuses a double-grant on the same UTC day. |
| `POST` | `/v1/pvp/match` | — | **INERT Phase 2 placeholder.** Returns `501`. Never wired in Phase 1. |

All authed routes expect `Authorization: Bearer <token>` from `/v1/accounts`.
Error responses are `{ "error": { "code", "message" } }`. Retry-denial messages
follow the calm, second-person UI tone (source-of-truth §12) — e.g.
`"You cannot retry a match you won."`

### Retry-token rules (enforced server-side — source-of-truth §6 Product 3)
- **Cannot retry a win.**
- **Cannot retry a retry.**
- **Rewards capped at the first-attempt rate.**
- Daily login grant: **+1/day cap 3** (F2P) · **+1/day cap 5** with Keepfall Plus.
- Shard pricing: **20 Shards** each / **90 Shards for 5**.

### Trust commitments
- **Cosmetics earned during a subscription are kept on cancellation.** On expiry
  / refund the entitlement flips `active:false` but `/v1/receipts/validate`
  returns `retainedCosmetics:true`. Covered by `test/receipts.test.ts`.
- Exactly **two currencies** (Stone, Shards). No tile is ever granted by spend.
  No subscriber-only units/tiles/combat advantage. One subscription tier only.

---

## Run the tests

```bash
npm install
npm test          # vitest run — 48 tests across retry / receipts / saves
npm run typecheck # tsc --noEmit
```

Tests run inside the real `workerd` runtime via
`@cloudflare/vitest-pool-workers`, with the D1 schema applied per file from
`src/db/migrations` (see `test/apply-migrations.ts`). The StoreKit verifier is
**injected** in tests, so entitlement logic is exercised without calling Apple.

> **Path note:** `workerd`'s module loader does not handle spaces in the
> repository's absolute path. If `npm test` errors with `No such module ".../%20..."`,
> run it from a space-free checkout (e.g. `cp -R` the `backend/` dir to `/tmp`).
> The code and tests are unaffected; this is a runtime path limitation only.

The pure authority logic lives in `src/lib/retry.ts` and the entitlement
decisions in `src/lib/receipts.ts` — both are exhaustively unit-tested.

---

## D1 setup

```bash
# 1. Create the database, then paste its id into wrangler.toml (database_id).
npx wrangler d1 create keepfall

# 2. Apply migrations (local dev DB).
npm run db:migrate
#   prod:  npx wrangler d1 migrations apply DB --remote --env production
```

`src/db/schema.sql` is the canonical, commented schema. `src/db/migrations/0001_init.sql`
mirrors it and is what wrangler applies. Keep the two in lockstep.

---

## Secrets (never committed)

Set with `wrangler secret put <NAME>` (add `--env production` for prod). They are
listed in `wrangler.toml` for reference but live only in Cloudflare.

| Secret | Purpose |
| --- | --- |
| `AUTH_HMAC_SECRET` | HMAC key signing/verifying bearer session tokens (`src/lib/auth.ts`). |
| `APPLE_SHARED_SECRET` | App Store Connect shared secret (legacy `verifyReceipt` fallback). |
| `APPLE_ROOT_CERT` | Apple Root CA - G3 (PEM) for verifying the StoreKit 2 JWS x5c chain. |

```bash
npx wrangler secret put AUTH_HMAC_SECRET
npx wrangler secret put APPLE_SHARED_SECRET
npx wrangler secret put APPLE_ROOT_CERT
```

Non-secret config (Plus product id, retry caps, Shard costs, bundle id) lives in
the `wrangler.toml` `[vars]` block and traces to source-of-truth §6.

---

## Deploy

```bash
npm run deploy                         # default env
npx wrangler deploy --env production    # production
```

> **Before production ship:** enable the StoreKit 2 JWS x5c chain verification in
> `src/lib/receipts.ts` (`appleStoreKitVerifier`). Until then the verifier hard-
> stops in `production` rather than trusting unverified payloads.

---

## Scripts

| Script | Action |
| --- | --- |
| `npm run dev` | `wrangler dev` (local Worker). |
| `npm test` | `vitest run`. |
| `npm run typecheck` | `tsc --noEmit`. |
| `npm run deploy` | `wrangler deploy`. |
| `npm run db:migrate` | `wrangler d1 migrations apply DB --local`. |
