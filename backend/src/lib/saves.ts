// Keepfall Phase 1 — cloud save (last-write-wins).
//
// The backend stores ONE opaque save blob per account and never parses gameplay
// out of it (§11 — cloud save only). Conflict resolution is last-write-wins by
// the client-asserted updated_utc; ties are broken in favour of the incoming
// write (the device that just wrote wins). schema_version is stored as-is so an
// older client can detect a newer save and migrate/refuse.

import type { CloudSaveRow } from "./db";

export interface IncomingSave {
  blob: string;
  schemaVersion: number;
  updatedUtc: number;
  deviceId: string;
}

export type PushDecision =
  | { action: "insert" }
  | { action: "overwrite" }
  | { action: "keep_existing"; reason: "stale_write" };

/**
 * decidePush — PURE last-write-wins resolver.
 *
 *   - No existing save → insert.
 *   - Incoming updated_utc >  existing → overwrite.
 *   - Incoming updated_utc <  existing → keep existing (stale write rejected).
 *   - Equal timestamps → overwrite (incoming device wins the tie; this lets a
 *     device re-push its own save without being told it's stale).
 *
 * Pure so it is unit-tested directly (saves.test.ts).
 */
export function decidePush(
  existing: Pick<CloudSaveRow, "updated_utc"> | null,
  incoming: Pick<IncomingSave, "updatedUtc">,
): PushDecision {
  if (existing == null) return { action: "insert" };
  if (incoming.updatedUtc < existing.updated_utc) {
    return { action: "keep_existing", reason: "stale_write" };
  }
  if (incoming.updatedUtc > existing.updated_utc) {
    return { action: "overwrite" };
  }
  return { action: "overwrite" }; // tie → incoming wins
}

/**
 * pushSave — apply a save with last-write-wins. Returns the row that is now
 * authoritative (either the freshly written incoming save or the retained
 * existing one) plus whether the write was applied.
 */
export async function pushSave(
  db: D1Database,
  accountId: string,
  incoming: IncomingSave,
): Promise<{ applied: boolean; row: CloudSaveRow }> {
  const existing = await db
    .prepare(`SELECT * FROM cloud_saves WHERE account_id = ?1`)
    .bind(accountId)
    .first<CloudSaveRow>();

  const decision = decidePush(existing, incoming);

  if (decision.action === "keep_existing") {
    // existing is non-null here by construction of decidePush.
    return { applied: false, row: existing as CloudSaveRow };
  }

  // INSERT or OVERWRITE: account_id is UNIQUE, so upsert in one statement.
  await db
    .prepare(
      `INSERT INTO cloud_saves
         (account_id, blob, schema_version, updated_utc, device_id)
       VALUES (?1, ?2, ?3, ?4, ?5)
       ON CONFLICT (account_id) DO UPDATE SET
         blob = excluded.blob,
         schema_version = excluded.schema_version,
         updated_utc = excluded.updated_utc,
         device_id = excluded.device_id`,
    )
    .bind(
      accountId,
      incoming.blob,
      incoming.schemaVersion,
      incoming.updatedUtc,
      incoming.deviceId,
    )
    .run();

  return {
    applied: true,
    row: {
      account_id: accountId,
      blob: incoming.blob,
      schema_version: incoming.schemaVersion,
      updated_utc: incoming.updatedUtc,
      device_id: incoming.deviceId,
    },
  };
}

/** Pull the latest cloud save for an account, or null if none exists yet. */
export async function pullSave(
  db: D1Database,
  accountId: string,
): Promise<CloudSaveRow | null> {
  return db
    .prepare(`SELECT * FROM cloud_saves WHERE account_id = ?1`)
    .bind(accountId)
    .first<CloudSaveRow>();
}
