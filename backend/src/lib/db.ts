// Keepfall Phase 1 — typed D1 helpers.
//
// Thin, typed wrappers around env.DB. Row shapes mirror src/db/schema.sql.
// Keeps SQL in one place so the route handlers stay readable and the pure logic
// modules stay DB-free.

// ── Row types (mirror schema.sql) ────────────────────────────────────────────

export interface AccountRow {
  id: string;
  apple_user_id: string;
  created_utc: number;
}

export interface CloudSaveRow {
  account_id: string;
  blob: string;
  schema_version: number;
  updated_utc: number;
  device_id: string;
}

export interface ReceiptRow {
  id: string;
  account_id: string;
  product_id: string;
  transaction_id: string;
  original_transaction_id: string;
  type: "consumable" | "subscription" | "nonconsumable";
  verified_utc: number;
  raw: string;
}

export interface EntitlementRow {
  account_id: string;
  product_id: string;
  kind: "subscription" | "nonconsumable";
  active: number; // 0/1
  period_end_utc: number | null;
}

export interface MatchAttemptRow {
  id: string;
  account_id: string;
  match_seed: string;
  ai_tier: number;
  started_utc: number;
  result: "pending" | "win" | "loss";
  is_retry: number; // 0/1
  parent_attempt_id: string | null;
  reward_stone: number;
  first_attempt_reward_stone: number;
}

export interface RetryLedgerRow {
  id: string;
  account_id: string;
  delta: number;
  source: "login" | "plus" | "pass" | "purchase" | "redeem";
  balance_after: number;
  created_utc: number;
}

// ── Helpers ──────────────────────────────────────────────────────────────────

/** Wall-clock now in epoch ms. Centralised so handlers never trust the client. */
export function nowMs(): number {
  return Date.now();
}

/** Crypto-random opaque id (uuid v4). Used for all primary keys we mint. */
export function newId(): string {
  return crypto.randomUUID();
}

/**
 * Current retry-token balance for an account — the balance_after of the latest
 * ledger row, or 0 if the account has no ledger history yet. The retry_ledger is
 * the sole authority for this number (§6 Product 3).
 */
export async function getRetryBalance(
  db: D1Database,
  accountId: string,
): Promise<number> {
  const row = await db
    .prepare(
      `SELECT balance_after FROM retry_ledger
       WHERE account_id = ?1
       ORDER BY created_utc DESC, rowid DESC
       LIMIT 1`,
    )
    .bind(accountId)
    .first<{ balance_after: number }>();
  return row?.balance_after ?? 0;
}

/** Append a retry-ledger row carrying the post-change balance. */
export function insertRetryLedgerStmt(
  db: D1Database,
  row: RetryLedgerRow,
): D1PreparedStatement {
  return db
    .prepare(
      `INSERT INTO retry_ledger
         (id, account_id, delta, source, balance_after, created_utc)
       VALUES (?1, ?2, ?3, ?4, ?5, ?6)`,
    )
    .bind(
      row.id,
      row.account_id,
      row.delta,
      row.source,
      row.balance_after,
      row.created_utc,
    );
}

/** Has this account already received a daily-login grant on the given UTC day? */
export async function hasDailyGrantOnDay(
  db: D1Database,
  accountId: string,
  utcDayStartMs: number,
  utcDayEndMs: number,
): Promise<boolean> {
  const row = await db
    .prepare(
      `SELECT 1 AS hit FROM retry_ledger
       WHERE account_id = ?1
         AND source IN ('login', 'plus')
         AND created_utc >= ?2 AND created_utc < ?3
       LIMIT 1`,
    )
    .bind(accountId, utcDayStartMs, utcDayEndMs)
    .first<{ hit: number }>();
  return row != null;
}

/** Look up a single match attempt by id (scoped to the owning account). */
export async function getAttempt(
  db: D1Database,
  accountId: string,
  attemptId: string,
): Promise<MatchAttemptRow | null> {
  return db
    .prepare(
      `SELECT * FROM match_attempts WHERE id = ?1 AND account_id = ?2`,
    )
    .bind(attemptId, accountId)
    .first<MatchAttemptRow>();
}

/** True if a receipt with this transaction_id already exists (idempotency). */
export async function receiptExists(
  db: D1Database,
  transactionId: string,
): Promise<boolean> {
  const row = await db
    .prepare(`SELECT 1 AS hit FROM receipts WHERE transaction_id = ?1 LIMIT 1`)
    .bind(transactionId)
    .first<{ hit: number }>();
  return row != null;
}
