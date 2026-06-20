// Keepfall Phase 1 — account upsert.
//
// Identity is Sign in with Apple's stable subject (apple_user_id). We upsert:
// first sign-in mints an account; later sign-ins return the existing one. No
// email, no social graph (Phase 1 has no friend graph — anti-pattern §10.10).

import type { AccountRow } from "./db";
import { newId, nowMs } from "./db";

/**
 * Upsert an account by Apple user id. Idempotent: returns the existing account
 * if one exists, otherwise creates it. INSERT ... ON CONFLICT keeps it a single
 * round-trip and race-safe under the unique index on apple_user_id.
 */
export async function upsertAccount(
  db: D1Database,
  appleUserId: string,
): Promise<AccountRow> {
  const id = newId();
  const created = nowMs();

  // On conflict we no-op the unique column so the existing row is returned by
  // the following SELECT. RETURNING is supported by D1's SQLite, but we SELECT
  // explicitly to get the canonical (possibly pre-existing) row.
  await db
    .prepare(
      `INSERT INTO accounts (id, apple_user_id, created_utc)
       VALUES (?1, ?2, ?3)
       ON CONFLICT (apple_user_id) DO NOTHING`,
    )
    .bind(id, appleUserId, created)
    .run();

  const row = await db
    .prepare(`SELECT * FROM accounts WHERE apple_user_id = ?1`)
    .bind(appleUserId)
    .first<AccountRow>();

  if (row == null) {
    // Should be unreachable: we just inserted or it already existed.
    throw new Error("account upsert failed to resolve a row");
  }
  return row;
}
