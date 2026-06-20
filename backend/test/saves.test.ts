// Keepfall Phase 1 — cloud save tests.
//
// Covers: last-write-wins by updated_utc, schema_version stored/respected.
// The pure resolver decidePush is tested directly; the round-trip is tested
// against D1 through pushSave/pullSave.

import { describe, it, expect } from "vitest";
import { env } from "cloudflare:test";
import { decidePush, pushSave, pullSave } from "../src/lib/saves";
import { upsertAccount } from "../src/lib/accounts";

describe("decidePush — pure last-write-wins resolver", () => {
  it("inserts when there is no existing save", () => {
    expect(decidePush(null, { updatedUtc: 100 })).toEqual({ action: "insert" });
  });

  it("overwrites when the incoming save is newer", () => {
    expect(decidePush({ updated_utc: 100 }, { updatedUtc: 200 })).toEqual({
      action: "overwrite",
    });
  });

  it("keeps existing when the incoming save is older (stale write)", () => {
    expect(decidePush({ updated_utc: 200 }, { updatedUtc: 100 })).toEqual({
      action: "keep_existing",
      reason: "stale_write",
    });
  });

  it("overwrites on a tie (incoming device wins)", () => {
    expect(decidePush({ updated_utc: 100 }, { updatedUtc: 100 })).toEqual({
      action: "overwrite",
    });
  });
});

describe("pushSave / pullSave — round-trip against D1", () => {
  async function freshAccount(): Promise<string> {
    const acct = await upsertAccount(env.DB, `apple-save-${crypto.randomUUID()}`);
    return acct.id;
  }

  it("stores and returns a save, preserving schema_version", async () => {
    const accountId = await freshAccount();
    const r = await pushSave(env.DB, accountId, {
      blob: '{"stone":120}',
      schemaVersion: 7,
      updatedUtc: 1000,
      deviceId: "iphone-a",
    });
    expect(r.applied).toBe(true);

    const pulled = await pullSave(env.DB, accountId);
    expect(pulled?.blob).toBe('{"stone":120}');
    expect(pulled?.schema_version).toBe(7);
    expect(pulled?.updated_utc).toBe(1000);
    expect(pulled?.device_id).toBe("iphone-a");
  });

  it("last-write-wins: a newer save overwrites an older one", async () => {
    const accountId = await freshAccount();
    await pushSave(env.DB, accountId, {
      blob: "old",
      schemaVersion: 1,
      updatedUtc: 1000,
      deviceId: "iphone-a",
    });
    const newer = await pushSave(env.DB, accountId, {
      blob: "new",
      schemaVersion: 2,
      updatedUtc: 2000,
      deviceId: "iphone-b",
    });
    expect(newer.applied).toBe(true);

    const pulled = await pullSave(env.DB, accountId);
    expect(pulled?.blob).toBe("new");
    expect(pulled?.schema_version).toBe(2);
    expect(pulled?.device_id).toBe("iphone-b");
  });

  it("last-write-wins: a stale (older) save is rejected and existing is kept", async () => {
    const accountId = await freshAccount();
    await pushSave(env.DB, accountId, {
      blob: "current",
      schemaVersion: 5,
      updatedUtc: 5000,
      deviceId: "iphone-a",
    });
    const stale = await pushSave(env.DB, accountId, {
      blob: "stale",
      schemaVersion: 4,
      updatedUtc: 4000, // older
      deviceId: "ipad-x",
    });
    expect(stale.applied).toBe(false);

    const pulled = await pullSave(env.DB, accountId);
    expect(pulled?.blob).toBe("current");
    expect(pulled?.schema_version).toBe(5);
    expect(pulled?.device_id).toBe("iphone-a");
  });

  it("returns null when an account has no save yet", async () => {
    const accountId = await freshAccount();
    expect(await pullSave(env.DB, accountId)).toBeNull();
  });
});
