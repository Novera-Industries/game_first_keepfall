// Keepfall Phase 1 — receipt / entitlement tests.
//
// Covers (source-of-truth §6):
//   - consumable idempotency by transaction_id (full route, against D1)
//   - subscription sets active entitlement + period_end (pure decision)
//   - cancellation/expiry flips active=false but DOES NOT revoke cosmetics
//     (the route returns retainedCosmetics so the client keeps them)
//
// The pure entitlement logic is tested directly. The idempotency path is tested
// end-to-end through the router with an INJECTED stub verifier (no Apple calls).

import { describe, it, expect } from "vitest";
import { env } from "cloudflare:test";
import {
  decideConsumable,
  decideEntitlement,
  isPlusActive,
  type JwsVerifier,
  type VerifiedTransaction,
} from "../src/lib/receipts";
import { SHARD_PACKS } from "../src/lib/config";
import { route, type Deps } from "../src/index";

const PLUS = env.PLUS_PRODUCT_ID;
const NOW = Date.parse("2026-06-20T12:00:00.000Z");

// ── pure entitlement decisions ───────────────────────────────────────────────

describe("decideEntitlement — subscription state", () => {
  it("active subscription: active=true, period_end set", () => {
    const txn: VerifiedTransaction = {
      productId: PLUS,
      transactionId: "t-1",
      originalTransactionId: "o-1",
      type: "subscription",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW,
      expiresDateMs: NOW + 30 * 86_400_000, // 30 days out
    };
    const d = decideEntitlement(txn, NOW);
    expect(d.active).toBe(true);
    expect(d.periodEndUtc).toBe(NOW + 30 * 86_400_000);
    expect(d.kind).toBe("subscription");
    expect(d.retainCosmetics).toBe(true);
  });

  it("expired subscription: active=false but cosmetics RETAINED", () => {
    const txn: VerifiedTransaction = {
      productId: PLUS,
      transactionId: "t-2",
      originalTransactionId: "o-1",
      type: "subscription",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW - 60 * 86_400_000,
      expiresDateMs: NOW - 86_400_000, // expired yesterday
    };
    const d = decideEntitlement(txn, NOW);
    expect(d.active).toBe(false);
    // Trust commitment: cosmetics are KEPT on expiry. (§6)
    expect(d.retainCosmetics).toBe(true);
  });

  it("revoked/refunded subscription: active=false, cosmetics RETAINED", () => {
    const txn: VerifiedTransaction = {
      productId: PLUS,
      transactionId: "t-3",
      originalTransactionId: "o-1",
      type: "subscription",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW,
      expiresDateMs: NOW + 30 * 86_400_000,
      revoked: true,
    };
    const d = decideEntitlement(txn, NOW);
    expect(d.active).toBe(false);
    expect(d.retainCosmetics).toBe(true);
  });

  it("non-consumable: permanently active, no period_end", () => {
    const txn: VerifiedTransaction = {
      productId: "com.vyradata.keepfall.cosmetic.border01",
      transactionId: "t-4",
      originalTransactionId: "t-4",
      type: "nonconsumable",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW,
    };
    const d = decideEntitlement(txn, NOW);
    expect(d.active).toBe(true);
    expect(d.periodEndUtc).toBeNull();
  });
});

describe("isPlusActive — Plus tier detection", () => {
  it("true only for the active Plus product", () => {
    const active = decideEntitlement(
      {
        productId: PLUS,
        transactionId: "t",
        originalTransactionId: "o",
        type: "subscription",
        bundleId: env.APP_BUNDLE_ID,
        purchaseDateMs: NOW,
        expiresDateMs: NOW + 86_400_000,
      },
      NOW,
    );
    expect(isPlusActive(active, env)).toBe(true);
  });

  it("false for a different product id", () => {
    const other = decideEntitlement(
      {
        productId: "com.vyradata.keepfall.something.else",
        transactionId: "t",
        originalTransactionId: "o",
        type: "subscription",
        bundleId: env.APP_BUNDLE_ID,
        purchaseDateMs: NOW,
        expiresDateMs: NOW + 86_400_000,
      },
      NOW,
    );
    expect(isPlusActive(other, env)).toBe(false);
  });

  it("false when null", () => {
    expect(isPlusActive(null, env)).toBe(false);
  });
});

// ── pure consumable decisions (server-authoritative Shard grant, §7) ──────────

describe("decideConsumable — Shard grant + unknown-product rejection", () => {
  const STARTER = "com.vyradata.keepfall.shards.starter";

  function consumableTxn(productId: string): VerifiedTransaction {
    return {
      productId,
      transactionId: "t-c",
      originalTransactionId: "o-c",
      type: "consumable",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW,
    };
  }

  it("known Shard pack returns the catalog grant (100 for starter)", () => {
    const out = decideConsumable(consumableTxn(STARTER), false);
    expect(out.status).toBe("consumable");
    if (out.status === "consumable") {
      expect(out.shardsGranted).toBe(SHARD_PACKS[STARTER]);
      expect(out.shardsGranted).toBe(100);
      expect(out.alreadyProcessed).toBe(false);
    }
  });

  it("every canonical pack maps to its catalog grant", () => {
    for (const [productId, shards] of Object.entries(SHARD_PACKS)) {
      const out = decideConsumable(consumableTxn(productId), false);
      expect(out.status).toBe("consumable");
      if (out.status === "consumable") {
        expect(out.shardsGranted).toBe(shards);
      }
    }
  });

  it("a replay still reports the grant but flags alreadyProcessed=true", () => {
    const out = decideConsumable(consumableTxn(STARTER), true);
    expect(out.status).toBe("consumable");
    if (out.status === "consumable") {
      expect(out.shardsGranted).toBe(100);
      expect(out.alreadyProcessed).toBe(true);
    }
  });

  it("an unknown consumable productId is rejected('unknown_product')", () => {
    const out = decideConsumable(
      consumableTxn("com.vyradata.keepfall.shards.bogus"),
      false,
    );
    expect(out.status).toBe("rejected");
    if (out.status === "rejected") {
      expect(out.reason).toBe("unknown_product");
    }
  });
});

// ── full-route idempotency + cancellation, against D1 ────────────────────────

/** A verifier stub that returns whatever transaction the test hands it. */
function stubVerifier(txn: VerifiedTransaction): Deps {
  const verifier: JwsVerifier = {
    async verify() {
      return txn;
    },
  };
  return { verifier };
}

async function makeAccountAndToken(): Promise<{ accountId: string; token: string }> {
  const res = await route(
    new Request("https://api.test/v1/accounts", {
      method: "POST",
      body: JSON.stringify({ appleUserId: `apple-${crypto.randomUUID()}` }),
    }),
    env,
    stubVerifier({} as VerifiedTransaction),
  );
  const body = (await res.json()) as { accountId: string; token: string };
  return body;
}

function validateReq(token: string): Request {
  return new Request("https://api.test/v1/receipts/validate", {
    method: "POST",
    headers: { authorization: `Bearer ${token}` },
    body: JSON.stringify({ signedTransaction: "stub.jws.payload" }),
  });
}

describe("POST /v1/receipts/validate — consumable Shard grant + idempotency", () => {
  it("known Shard pack returns the catalog grant; a replay flags alreadyProcessed and does not double-record", async () => {
    const { token } = await makeAccountAndToken();
    const txn: VerifiedTransaction = {
      productId: "com.vyradata.keepfall.shards.starter",
      transactionId: `txn-${crypto.randomUUID()}`,
      originalTransactionId: "o-consumable",
      type: "consumable",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW,
    };
    const deps = stubVerifier(txn);

    const first = await route(validateReq(token), env, deps);
    const firstBody = (await first.json()) as {
      status: string;
      shardsGranted: number;
      alreadyProcessed: boolean;
    };
    expect(first.status).toBe(200);
    expect(firstBody.status).toBe("consumable");
    // Server-authoritative grant from the catalog (§7).
    expect(firstBody.shardsGranted).toBe(100);
    expect(firstBody.alreadyProcessed).toBe(false);

    // Replay the exact same transaction id → idempotent, alreadyProcessed=true.
    // The grant is still reported, but the client must not double-credit.
    const second = await route(validateReq(token), env, deps);
    const secondBody = (await second.json()) as {
      shardsGranted: number;
      alreadyProcessed: boolean;
    };
    expect(secondBody.alreadyProcessed).toBe(true);
    expect(secondBody.shardsGranted).toBe(100);

    // Exactly one receipt row exists for this transaction.
    const count = await env.DB.prepare(
      `SELECT COUNT(*) AS n FROM receipts WHERE transaction_id = ?1`,
    )
      .bind(txn.transactionId)
      .first<{ n: number }>();
    expect(count?.n).toBe(1);
  });

  it("an unknown consumable productId is rejected and never recorded", async () => {
    const { token } = await makeAccountAndToken();
    const txnId = `txn-${crypto.randomUUID()}`;
    const txn: VerifiedTransaction = {
      productId: "com.vyradata.keepfall.shards.bogus",
      transactionId: txnId,
      originalTransactionId: "o-bogus",
      type: "consumable",
      bundleId: env.APP_BUNDLE_ID,
      purchaseDateMs: NOW,
    };

    const res = await route(validateReq(token), env, stubVerifier(txn));
    expect(res.status).toBe(422);
    const body = (await res.json()) as { error: { code: string } };
    expect(body.error.code).toBe("unknown_product");

    // No receipt row was written for the rejected product.
    const count = await env.DB.prepare(
      `SELECT COUNT(*) AS n FROM receipts WHERE transaction_id = ?1`,
    )
      .bind(txnId)
      .first<{ n: number }>();
    expect(count?.n).toBe(0);
  });
});

describe("POST /v1/receipts/validate — subscription active then cancelled", () => {
  it("active Plus → entitlement active; expiry flips active=false, cosmetics kept", async () => {
    const { token } = await makeAccountAndToken();
    const txnId = `txn-${crypto.randomUUID()}`;

    // 1. Active subscription.
    const activeRes = await route(
      validateReq(token),
      env,
      stubVerifier({
        productId: PLUS,
        transactionId: txnId,
        originalTransactionId: "o-sub-1",
        type: "subscription",
        bundleId: env.APP_BUNDLE_ID,
        purchaseDateMs: NOW,
        expiresDateMs: Date.now() + 30 * 86_400_000,
      }),
    );
    const activeBody = (await activeRes.json()) as {
      status: string;
      active: boolean;
      isPlus: boolean;
      retainedCosmetics: boolean;
    };
    expect(activeBody.status).toBe("entitlement");
    expect(activeBody.active).toBe(true);
    expect(activeBody.isPlus).toBe(true);
    expect(activeBody.retainedCosmetics).toBe(true);

    // 2. Same subscription lineage, now expired (renewal info update).
    const expiredRes = await route(
      validateReq(token),
      env,
      stubVerifier({
        productId: PLUS,
        transactionId: `${txnId}-renewal`,
        originalTransactionId: "o-sub-1",
        type: "subscription",
        bundleId: env.APP_BUNDLE_ID,
        purchaseDateMs: NOW,
        expiresDateMs: Date.now() - 86_400_000, // expired
      }),
    );
    const expiredBody = (await expiredRes.json()) as {
      active: boolean;
      retainedCosmetics: boolean;
    };
    expect(expiredBody.active).toBe(false);
    // The non-negotiable trust commitment.
    expect(expiredBody.retainedCosmetics).toBe(true);
  });
});
