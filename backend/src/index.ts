// Keepfall Phase 1 — Cloudflare Worker entrypoint + router.
//
// Scope: accounts + cloud save + StoreKit 2 receipt validation + RETRY-TOKEN
// AUTHORITY. No gameplay simulation. Single-player PvE only. (source-of-truth §11)
//
// No heavy framework — a small method+path switch. Financial/authority logic
// lives in the pure modules under src/lib and is covered by tests; this file is
// the thin I/O shell that validates input, calls those functions, and persists
// their decisions atomically.

import type { Env } from "./lib/config";
import { envInt, plusProductId } from "./lib/config";
import {
  authedAccountId,
  issueToken,
  sessionTtlSeconds,
} from "./lib/auth";
import { upsertAccount } from "./lib/accounts";
import { pushSave, pullSave } from "./lib/saves";
import {
  appleStoreKitVerifier,
  decideEntitlement,
  isPlusActive,
  type JwsVerifier,
} from "./lib/receipts";
import {
  applyRedeem,
  canRedeem,
  dailyGrantAmount,
  utcDayKey,
  type AttemptSnapshot,
} from "./lib/retry";
import {
  getAttempt,
  getRetryBalance,
  hasDailyGrantOnDay,
  insertRetryLedgerStmt,
  newId,
  nowMs,
  receiptExists,
  type EntitlementRow,
  type MatchAttemptRow,
} from "./lib/db";

// ── tiny response helpers ────────────────────────────────────────────────────

const json = (data: unknown, status = 200): Response =>
  new Response(JSON.stringify(data), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });

const err = (status: number, code: string, message: string): Response =>
  json({ error: { code, message } }, status);

// ── PvP placeholder (Phase 2) ────────────────────────────────────────────────
// INERT. Phase 1 is single-player PvE only (source-of-truth §0). This hook exists
// so the route table has a documented home for Phase 2 async PvP and is never
// wired to logic. It must stay disabled and return 501.
function pvpPlaceholder(): Response {
  // PHASE-2-PLACEHOLDER: do not implement in Phase 1.
  return err(501, "not_implemented", "PvP arrives in Phase 2. Phase 1 is single-player.");
}

/**
 * The verifier is injectable so tests can supply a stub. Production uses the
 * real StoreKit 2 JWS verifier.
 */
export interface Deps {
  verifier: JwsVerifier;
}

const defaultDeps: Deps = { verifier: appleStoreKitVerifier };

export default {
  async fetch(req: Request, env: Env, _ctx: ExecutionContext): Promise<Response> {
    return route(req, env, defaultDeps);
  },
};

/** Exported for tests so a stub verifier can be injected. */
export async function route(req: Request, env: Env, deps: Deps): Promise<Response> {
  const url = new URL(req.url);
  const path = url.pathname;
  const method = req.method.toUpperCase();
  const key = `${method} ${path}`;

  try {
    switch (key) {
      case "GET /v1/health":
        return json({ ok: true, service: "keepfall-api", environment: env.ENVIRONMENT });

      case "POST /v1/accounts":
        return handleAccounts(req, env);

      case "POST /v1/save":
        return handleSavePush(req, env);
      case "GET /v1/save":
        return handleSavePull(req, env);

      case "POST /v1/receipts/validate":
        return handleReceiptValidate(req, env, deps);

      case "POST /v1/matches/start":
        return handleMatchStart(req, env);
      case "POST /v1/matches/result":
        return handleMatchResult(req, env);

      case "POST /v1/retry/request":
        return handleRetryRequest(req, env);
      case "POST /v1/retry/redeem":
        return handleRetryRedeem(req, env);
      case "POST /v1/retry/grant-daily":
        return handleRetryGrantDaily(req, env);

      // INERT Phase 2 placeholder — single-player only in Phase 1.
      case "POST /v1/pvp/match":
        return pvpPlaceholder();

      default:
        return err(404, "not_found", `No route for ${key}`);
    }
  } catch (e) {
    const message = e instanceof Error ? e.message : "unknown error";
    return err(500, "internal_error", message);
  }
}

// ── auth gate ────────────────────────────────────────────────────────────────

/** Resolve the caller's account id from the bearer token, or null (=> 401). */
async function requireAccount(req: Request, env: Env): Promise<string | null> {
  return authedAccountId(req.headers.get("authorization"), env.AUTH_HMAC_SECRET);
}

// ── POST /v1/accounts ────────────────────────────────────────────────────────

async function handleAccounts(req: Request, env: Env): Promise<Response> {
  const body = await readJson<{ appleUserId?: string }>(req);
  if (!body?.appleUserId) {
    return err(400, "bad_request", "appleUserId is required");
  }
  if (!env.AUTH_HMAC_SECRET) {
    return err(500, "config_error", "AUTH_HMAC_SECRET is not configured");
  }

  const account = await upsertAccount(env.DB, body.appleUserId);
  const ttl = sessionTtlSeconds(env);
  const token = await issueToken(account.id, env.AUTH_HMAC_SECRET, ttl);

  return json({ accountId: account.id, token, expiresInSeconds: ttl });
}

// ── POST /v1/save  (push, last-write-wins) ───────────────────────────────────

async function handleSavePush(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const body = await readJson<{
    blob?: string;
    schemaVersion?: number;
    updatedUtc?: number;
    deviceId?: string;
  }>(req);

  if (
    body?.blob == null ||
    typeof body.schemaVersion !== "number" ||
    typeof body.updatedUtc !== "number" ||
    !body.deviceId
  ) {
    return err(400, "bad_request", "blob, schemaVersion, updatedUtc, deviceId are required");
  }

  const { applied, row } = await pushSave(env.DB, accountId, {
    blob: body.blob,
    schemaVersion: body.schemaVersion,
    updatedUtc: body.updatedUtc,
    deviceId: body.deviceId,
  });

  return json({
    applied,
    // When not applied the caller's write was stale; echo the authoritative save
    // so the client can reconcile.
    save: {
      schemaVersion: row.schema_version,
      updatedUtc: row.updated_utc,
      deviceId: row.device_id,
    },
  });
}

// ── GET /v1/save  (pull latest) ──────────────────────────────────────────────

async function handleSavePull(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const row = await pullSave(env.DB, accountId);
  if (row == null) return json({ save: null });

  return json({
    save: {
      blob: row.blob,
      schemaVersion: row.schema_version,
      updatedUtc: row.updated_utc,
      deviceId: row.device_id,
    },
  });
}

// ── POST /v1/receipts/validate ───────────────────────────────────────────────

async function handleReceiptValidate(
  req: Request,
  env: Env,
  deps: Deps,
): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const body = await readJson<{ signedTransaction?: string }>(req);
  if (!body?.signedTransaction) {
    return err(400, "bad_request", "signedTransaction (StoreKit 2 JWS) is required");
  }

  // 1. Verify the transaction (real Apple verify in prod; stub in tests).
  let txn;
  try {
    txn = await deps.verifier.verify(body.signedTransaction, env);
  } catch (e) {
    const message = e instanceof Error ? e.message : "verification failed";
    return err(422, "receipt_invalid", message);
  }

  const now = nowMs();

  // 2. Idempotency: a receipt is keyed by transaction_id. Replaying never
  //    double-grants (consumables) or double-applies (subscriptions).
  const alreadyProcessed = await receiptExists(env.DB, txn.transactionId);

  if (!alreadyProcessed) {
    await env.DB.prepare(
      `INSERT INTO receipts
         (id, account_id, product_id, transaction_id, original_transaction_id,
          type, verified_utc, raw)
       VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)
       ON CONFLICT (transaction_id) DO NOTHING`,
    )
      .bind(
        newId(),
        accountId,
        txn.productId,
        txn.transactionId,
        txn.originalTransactionId,
        txn.type,
        now,
        body.signedTransaction,
      )
      .run();
  }

  // 3. Consumables (retry tokens, Shard packs) just record idempotently.
  if (txn.type === "consumable") {
    return json({
      status: "consumable",
      productId: txn.productId,
      transactionId: txn.transactionId,
      alreadyProcessed,
    });
  }

  // 4. Subscriptions / non-consumables → compute + upsert entitlement.
  //    Cosmetics earned during a subscription are KEPT on cancellation (§6):
  //    even when active flips to false we report retainedCosmetics:true.
  const decision = decideEntitlement(txn, now);
  await env.DB.prepare(
    `INSERT INTO entitlements (account_id, product_id, kind, active, period_end_utc)
     VALUES (?1, ?2, ?3, ?4, ?5)
     ON CONFLICT (account_id, product_id) DO UPDATE SET
       kind = excluded.kind,
       active = excluded.active,
       period_end_utc = excluded.period_end_utc`,
  )
    .bind(
      accountId,
      decision.productId,
      decision.kind,
      decision.active ? 1 : 0,
      decision.periodEndUtc,
    )
    .run();

  return json({
    status: "entitlement",
    productId: decision.productId,
    kind: decision.kind,
    active: decision.active,
    periodEndUtc: decision.periodEndUtc,
    isPlus: decision.productId === plusProductId(env),
    // Trust commitment surfaced to the client.
    retainedCosmetics: decision.retainCosmetics,
  });
}

// ── POST /v1/matches/start ───────────────────────────────────────────────────

async function handleMatchStart(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const body = await readJson<{ matchSeed?: string; aiTier?: number }>(req);
  if (!body?.matchSeed || typeof body.aiTier !== "number") {
    return err(400, "bad_request", "matchSeed and aiTier are required");
  }
  if (body.aiTier < 1 || body.aiTier > 5) {
    return err(400, "bad_request", "aiTier must be 1..5");
  }

  const id = newId();
  await env.DB.prepare(
    `INSERT INTO match_attempts
       (id, account_id, match_seed, ai_tier, started_utc, result,
        is_retry, parent_attempt_id, reward_stone, first_attempt_reward_stone)
     VALUES (?1, ?2, ?3, ?4, ?5, 'pending', 0, NULL, 0, 0)`,
  )
    .bind(id, accountId, body.matchSeed, body.aiTier, nowMs())
    .run();

  return json({ attemptId: id, result: "pending" });
}

// ── POST /v1/matches/result ──────────────────────────────────────────────────

async function handleMatchResult(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const body = await readJson<{
    attemptId?: string;
    result?: "win" | "loss";
    rewardStone?: number;
  }>(req);
  if (!body?.attemptId || (body.result !== "win" && body.result !== "loss")) {
    return err(400, "bad_request", "attemptId and result ('win'|'loss') are required");
  }

  const attempt = await getAttempt(env.DB, accountId, body.attemptId);
  if (attempt == null) return err(404, "not_found", "attempt not found");
  if (attempt.result !== "pending") {
    return err(409, "already_resolved", "attempt already has a result");
  }

  // First-attempt reward is recorded on the original attempt and inherited as the
  // cap by any retry. A retry's reward is clamped to its first_attempt cap.
  const claimed = Math.max(0, body.rewardStone ?? 0);
  const reward = attempt.is_retry === 1
    ? Math.min(claimed, attempt.first_attempt_reward_stone)
    : claimed;
  const firstAttemptReward = attempt.is_retry === 1
    ? attempt.first_attempt_reward_stone
    : reward;

  await env.DB.prepare(
    `UPDATE match_attempts
       SET result = ?1, reward_stone = ?2, first_attempt_reward_stone = ?3
     WHERE id = ?4 AND account_id = ?5`,
  )
    .bind(body.result, reward, firstAttemptReward, body.attemptId, accountId)
    .run();

  return json({ attemptId: body.attemptId, result: body.result, rewardStone: reward });
}

// ── POST /v1/retry/request  (eligibility check — READ-ONLY) ──────────────────
//
// Asks the authority whether an attempt MAY be retried, without spending a token
// or creating a retry attempt. Runs the SAME pure gate as /v1/retry/redeem
// (canRedeem) so the answer can never drift from what redeem would decide; it
// just does not mutate anything. The client uses this to decide whether to even
// offer the retry surface (source-of-truth §6 Product 3).

async function handleRetryRequest(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const body = await readJson<{ attemptId?: string }>(req);
  if (!body?.attemptId) {
    return err(400, "bad_request", "attemptId is required");
  }

  const attemptRow = await getAttempt(env.DB, accountId, body.attemptId);
  const tokenBalance = await getRetryBalance(env.DB, accountId);

  const snapshot: AttemptSnapshot | null = attemptRow
    ? toSnapshot(attemptRow)
    : null;

  // Pure authority gate — cannot retry a win, cannot retry a retry, must hold a
  // token. NO token is spent and NO retry attempt is created here.
  const gate = canRedeem(snapshot, tokenBalance);

  return json({
    eligible: gate.ok,
    reason: gate.ok ? null : gate.reason,
    tokenBalance,
  });
}

// ── POST /v1/retry/redeem  (THE AUTHORITY) ───────────────────────────────────

async function handleRetryRedeem(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const body = await readJson<{ attemptId?: string }>(req);
  if (!body?.attemptId) {
    return err(400, "bad_request", "attemptId is required");
  }

  const attemptRow = await getAttempt(env.DB, accountId, body.attemptId);
  const balance = await getRetryBalance(env.DB, accountId);

  const snapshot: AttemptSnapshot | null = attemptRow
    ? toSnapshot(attemptRow)
    : null;

  // Authority check — cannot retry a win, cannot retry a retry, must hold a token.
  const gate = canRedeem(snapshot, balance);
  if (!gate.ok) {
    const map: Record<string, number> = {
      cannot_retry_a_win: 409,
      cannot_retry_a_retry: 409,
      attempt_not_found: 404,
      attempt_not_resolved: 409,
      insufficient_tokens: 402,
    };
    return err(map[gate.reason] ?? 409, gate.reason, denialMessage(gate.reason));
  }

  // snapshot is non-null here (canRedeem rejected null).
  const result = applyRedeem(snapshot as AttemptSnapshot, balance);
  const now = nowMs();
  const retryId = newId();

  // Atomic: spend the token (ledger) AND create the identical retry attempt
  // with the first-attempt reward cap, in one D1 batch.
  await env.DB.batch([
    insertRetryLedgerStmt(env.DB, {
      id: newId(),
      account_id: accountId,
      delta: result.ledgerDelta, // -1
      source: "redeem",
      balance_after: result.newBalance,
      created_utc: now,
    }),
    env.DB.prepare(
      `INSERT INTO match_attempts
         (id, account_id, match_seed, ai_tier, started_utc, result,
          is_retry, parent_attempt_id, reward_stone, first_attempt_reward_stone)
       VALUES (?1, ?2, ?3, ?4, ?5, 'pending', 1, ?6, 0, ?7)`,
    ).bind(
      retryId,
      accountId,
      result.retryAttempt.matchSeed,
      result.retryAttempt.aiTier,
      now,
      result.retryAttempt.parentAttemptId,
      result.retryAttempt.rewardCapStone,
    ),
  ]);

  return json({
    retryAttemptId: retryId,
    // Identical conditions the client must reproduce.
    matchSeed: result.retryAttempt.matchSeed,
    aiTier: result.retryAttempt.aiTier,
    parentAttemptId: result.retryAttempt.parentAttemptId,
    rewardCapStone: result.retryAttempt.rewardCapStone,
    retryTokenBalance: result.newBalance,
  });
}

// ── POST /v1/retry/grant-daily  (idempotent daily grant) ─────────────────────

async function handleRetryGrantDaily(req: Request, env: Env): Promise<Response> {
  const accountId = await requireAccount(req, env);
  if (!accountId) return err(401, "unauthorized", "Valid bearer token required");

  const now = nowMs();
  const balance = await getRetryBalance(env.DB, accountId);

  // Plus headroom: active Keepfall Plus raises the daily cap to 5 (else 3).
  const isPlus = await accountHasActivePlus(env.DB, accountId, env, now);

  // One grant per UTC day — compute the day window [start, end).
  const dayStart = Date.parse(`${utcDayKey(now)}T00:00:00.000Z`);
  const dayEnd = dayStart + 86_400_000;
  const grantedToday = await hasDailyGrantOnDay(env.DB, accountId, dayStart, dayEnd);

  const amount = dailyGrantAmount(balance, isPlus, grantedToday);

  if (amount <= 0) {
    return json({
      granted: 0,
      retryTokenBalance: balance,
      capped: !grantedToday, // true => at cap; false => already granted today
      alreadyGrantedToday: grantedToday,
    });
  }

  const newBalance = balance + amount;
  await insertRetryLedgerStmt(env.DB, {
    id: newId(),
    account_id: accountId,
    delta: amount,
    // 'plus' when the Plus headroom enabled this grant, else 'login'.
    source: isPlus && balance >= 3 ? "plus" : "login",
    balance_after: newBalance,
    created_utc: now,
  }).run();

  return json({
    granted: amount,
    retryTokenBalance: newBalance,
    capped: false,
    alreadyGrantedToday: false,
  });
}

// ── helpers ──────────────────────────────────────────────────────────────────

function toSnapshot(row: MatchAttemptRow): AttemptSnapshot {
  return {
    id: row.id,
    result: row.result,
    isRetry: row.is_retry === 1,
    firstAttemptRewardStone: row.first_attempt_reward_stone,
    matchSeed: row.match_seed,
    aiTier: row.ai_tier,
  };
}

/** Is Keepfall Plus currently active for this account? (drives the +2 cap.) */
async function accountHasActivePlus(
  db: D1Database,
  accountId: string,
  env: Env,
  now: number,
): Promise<boolean> {
  const row = await db
    .prepare(
      `SELECT * FROM entitlements WHERE account_id = ?1 AND product_id = ?2`,
    )
    .bind(accountId, plusProductId(env))
    .first<EntitlementRow>();
  if (row == null) return false;

  // Re-derive activeness against "now" so a lapsed period without a webhook
  // update still reads as inactive.
  return isPlusActive(
    {
      productId: row.product_id,
      kind: "subscription",
      active: row.active === 1 && (row.period_end_utc == null || row.period_end_utc > now),
      periodEndUtc: row.period_end_utc,
      retainCosmetics: true,
    },
    env,
  );
}

function denialMessage(reason: string): string {
  switch (reason) {
    case "cannot_retry_a_win":
      return "You cannot retry a match you won.";
    case "cannot_retry_a_retry":
      return "You cannot retry a retry attempt.";
    case "attempt_not_found":
      return "That match attempt was not found.";
    case "attempt_not_resolved":
      return "That match has not finished yet.";
    case "insufficient_tokens":
      return "You have no retry tokens. You can earn one with a daily login.";
    default:
      return "This retry is not allowed.";
  }
}

async function readJson<T>(req: Request): Promise<T | null> {
  try {
    return (await req.json()) as T;
  } catch {
    return null;
  }
}

// Re-export envInt so callers importing from the entry can read config ints.
export { envInt };
