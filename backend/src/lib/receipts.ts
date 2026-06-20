// Keepfall Phase 1 — StoreKit 2 receipt validation + entitlement decisions.
//
// The Worker validates StoreKit 2 JWS transactions (or, as a documented fallback,
// the legacy verifyReceipt response) and records:
//   - a receipts row (idempotent by transaction_id),
//   - an entitlement row for subscriptions / non-consumables.
//
// (source-of-truth §6, §7 — receipts validated on Cloudflare Workers.)
//
// TESTABILITY: real Apple verification (JWS x5c chain → Apple Root CA - G3) is
// network/cert heavy. We inject a `JwsVerifier`, so the entitlement decision
// logic — the part with the trust commitments — is unit-tested without Apple.

import type { Env } from "./config";
import { plusProductId } from "./config";

/** Product categories we accept (mirrors the receipts.type CHECK). */
export type ProductType = "consumable" | "subscription" | "nonconsumable";

/**
 * The decoded, trustworthy fields of a StoreKit 2 transaction. This is what a
 * verifier returns AFTER it has checked the JWS signature chain and bundle id.
 */
export interface VerifiedTransaction {
  productId: string;
  transactionId: string;
  originalTransactionId: string;
  type: ProductType;
  bundleId: string;
  purchaseDateMs: number;
  /** Subscription renewal/expiry boundary in ms; undefined for one-time buys. */
  expiresDateMs?: number;
  /** True when the App Store reports this subscription as revoked/refunded. */
  revoked?: boolean;
}

/**
 * Pluggable verifier. Production wires `appleStoreKitVerifier`; tests inject a
 * stub that returns canned VerifiedTransactions so entitlement logic is tested
 * in isolation.
 */
export interface JwsVerifier {
  verify(signedTransaction: string, env: Env): Promise<VerifiedTransaction>;
}

/** Result of deciding entitlement state from a verified transaction. */
export interface EntitlementDecision {
  productId: string;
  kind: "subscription" | "nonconsumable";
  active: boolean;
  periodEndUtc: number | null;
  /**
   * Cosmetics earned during a subscription are KEPT on cancellation/expiry —
   * non-negotiable trust commitment (§6). When a subscription is inactive we
   * still report it so the client never strips earned cosmetics.
   */
  retainCosmetics: boolean;
}

/** Outcome classes returned to the route handler. */
export type ReceiptOutcome =
  | { status: "consumable"; productId: string; transactionId: string; alreadyProcessed: boolean }
  | { status: "entitlement"; decision: EntitlementDecision }
  | { status: "rejected"; reason: string };

/**
 * decideEntitlement — PURE. Given a verified transaction and "now", compute the
 * entitlement row to upsert. Holds the subscription trust rules.
 *
 *   - subscription: active iff not revoked AND expiresDate is in the future.
 *     On expiry/cancellation/refund → active:false, but retainCosmetics stays
 *     true so the client keeps cosmetics earned during the paid period (§6).
 *   - nonconsumable: permanently active once owned (no period_end).
 *
 * Consumables do NOT produce an entitlement (handled separately, idempotently).
 */
export function decideEntitlement(
  txn: VerifiedTransaction,
  nowMs: number,
): EntitlementDecision {
  if (txn.type === "subscription") {
    const periodEnd = txn.expiresDateMs ?? null;
    const notExpired = periodEnd != null && periodEnd > nowMs;
    const active = !txn.revoked && notExpired;
    return {
      productId: txn.productId,
      kind: "subscription",
      active,
      periodEndUtc: periodEnd,
      // Always retain — whether active or lapsed, earned cosmetics are kept.
      retainCosmetics: true,
    };
  }

  // nonconsumable — owned forever.
  return {
    productId: txn.productId,
    kind: "nonconsumable",
    active: !txn.revoked,
    periodEndUtc: null,
    retainCosmetics: true,
  };
}

/**
 * isPlusActive — PURE convenience: is this decision the Keepfall Plus tier and
 * currently active? Used to grant the +2 retry cap headroom (§6 Product 3) and
 * Plus perks. Plus is the ONE subscription tier (§6 / anti-pattern §10.8).
 */
export function isPlusActive(
  decision: EntitlementDecision | null,
  env: Env,
): boolean {
  if (decision == null) return false;
  return decision.active && decision.productId === plusProductId(env);
}

/**
 * appleStoreKitVerifier — production JWS verifier scaffold.
 *
 * A StoreKit 2 signed transaction is a JWS (header.payload.signature). The header
 * carries an x5c certificate chain; full verification:
 *   1. base64url-decode the x5c leaf/intermediate/root certs,
 *   2. confirm the chain terminates at Apple Root CA - G3 (env.APPLE_ROOT_CERT),
 *   3. verify the JWS signature with the leaf cert's public key (ES256),
 *   4. confirm payload.bundleId === env.APP_BUNDLE_ID and the cert validity dates.
 *
 * Step 1 (decode + bundle/expiry mapping) is implemented so the pipeline runs
 * end to end against well-formed StoreKit 2 sandbox payloads. The x5c chain
 * cryptographic check (steps 2–3) is marked clearly and MUST be completed before
 * production: until then, set ENVIRONMENT != "production" so unsigned/sandbox
 * payloads are accepted only outside production. The legacy verifyReceipt path
 * (APPLE_SHARED_SECRET) remains available as a documented fallback.
 */
export const appleStoreKitVerifier: JwsVerifier = {
  async verify(signedTransaction: string, env: Env): Promise<VerifiedTransaction> {
    const parts = signedTransaction.split(".");
    if (parts.length !== 3) {
      throw new Error("malformed JWS: expected three segments");
    }
    const [, payloadB64] = parts;
    const payloadJson = base64UrlDecode(payloadB64);
    const p = JSON.parse(payloadJson) as Record<string, unknown>;

    // ── x5c chain verification (steps 2–3) ──────────────────────────────────
    // Production gate: refuse to trust unverified payloads in production until
    // the Apple Root CA - G3 chain check below is implemented.
    if (env.ENVIRONMENT === "production") {
      // TODO(before-prod-ship): verify parts[0].x5c chain against
      // env.APPLE_ROOT_CERT and check the ES256 signature over `${parts[0]}.${parts[1]}`.
      // Implemented intentionally as a hard stop, not a silent pass-through.
      throw new Error(
        "StoreKit 2 JWS chain verification not yet enabled in production",
      );
    }

    const bundleId = String(p["bundleId"] ?? "");
    if (bundleId !== env.APP_BUNDLE_ID) {
      throw new Error(
        `bundleId mismatch: ${bundleId} !== ${env.APP_BUNDLE_ID}`,
      );
    }

    return {
      productId: String(p["productId"] ?? ""),
      transactionId: String(p["transactionId"] ?? ""),
      originalTransactionId: String(
        p["originalTransactionId"] ?? p["transactionId"] ?? "",
      ),
      type: mapProductType(p["type"]),
      bundleId,
      purchaseDateMs: numberOr(p["purchaseDate"], Date.now()),
      ...(p["expiresDate"] != null
        ? { expiresDateMs: numberOr(p["expiresDate"], 0) }
        : {}),
      ...(p["revocationDate"] != null ? { revoked: true } : {}),
    };
  },
};

/** Map Apple's transaction `type` string to our ProductType. */
function mapProductType(value: unknown): ProductType {
  switch (String(value)) {
    case "Auto-Renewable Subscription":
    case "subscription":
      return "subscription";
    case "Non-Consumable":
    case "nonconsumable":
      return "nonconsumable";
    case "Consumable":
    case "consumable":
    default:
      return "consumable";
  }
}

function numberOr(value: unknown, fallback: number): number {
  const n = typeof value === "number" ? value : Number(value);
  return Number.isFinite(n) ? n : fallback;
}

/** base64url → utf-8 string, runtime-agnostic (atob exists in workerd). */
function base64UrlDecode(b64url: string): string {
  const b64 = b64url.replace(/-/g, "+").replace(/_/g, "/");
  const pad = b64.length % 4 === 0 ? "" : "=".repeat(4 - (b64.length % 4));
  const binary = atob(b64 + pad);
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}
