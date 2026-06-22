// Keepfall Phase 1 — server-side constants.
//
// Every value here traces to docs/00-source-of-truth.md. Do NOT invent values.
// Economy tuning that the client adjusts via Firebase Remote Config (§11) is not
// duplicated here; only the constants the Worker itself must enforce live here.

/** Worker environment bindings (from wrangler.toml [vars] + secrets). */
export interface Env {
  DB: D1Database;

  // [vars]
  ENVIRONMENT: string;
  APP_BUNDLE_ID: string;
  PLUS_PRODUCT_ID: string;
  RETRY_TOKEN_SHARD_COST: string;
  RETRY_TOKEN_BUNDLE_5_SHARD_COST: string;
  RETRY_DAILY_CAP_F2P: string;
  RETRY_DAILY_CAP_PLUS: string;
  SESSION_TTL_SECONDS: string;

  // secrets (wrangler secret put …) — optional so local/test runs work without them.
  APPLE_SHARED_SECRET?: string;
  APPLE_ROOT_CERT?: string;
  AUTH_HMAC_SECRET?: string;
}

// ── Currencies (source-of-truth §1) ─────────────────────────────────────────
// EXACTLY two currencies exist. There is never a third. (anti-pattern §10.9)
export const CURRENCIES = ["stone", "shards"] as const;
export type Currency = (typeof CURRENCIES)[number];

// ── Keepfall Plus subscription (source-of-truth §6 Product 2) ────────────────
// ONE tier only. $5.99/month. Multi-tier subscriptions are an anti-pattern (§10.8).
export const PLUS_PRICE_USD = 5.99;
export const PLUS_PRODUCT_FALLBACK_ID = "com.vyradata.keepfall.plus.monthly";

// ── Shard IAP pack ladder (source-of-truth §7) ───────────────────────────────
// The product→Shards mapping is SERVER-AUTHORITATIVE: the Worker validates the
// receipt and returns the Shard grant for the product; the client credits the
// server's amount, never its own. These five values are CANONICAL and mirror
// config/iap-catalog.json + unity/.../IapCatalog.cs EXACTLY — do not change a
// number without changing all three. Shards buy convenience + cosmetics only,
// never units, tiles, or power (§1, §6, §10).
export const SHARD_PACKS: Readonly<Record<string, number>> = {
  "com.vyradata.keepfall.shards.starter": 100,
  "com.vyradata.keepfall.shards.pouch": 550,
  "com.vyradata.keepfall.shards.chest": 1200,
  "com.vyradata.keepfall.shards.vault": 2600,
  "com.vyradata.keepfall.shards.hoard": 7000,
};

/**
 * shardsForProduct — how many Shards a validated consumable purchase grants, or
 * null when the productId is not a known Shard pack. A null result means the
 * Worker must REJECT the receipt as an unknown product rather than grant Shards.
 */
export function shardsForProduct(productId: string): number | null {
  return Object.prototype.hasOwnProperty.call(SHARD_PACKS, productId)
    ? SHARD_PACKS[productId]
    : null;
}

// ── PvE Retry Tokens (source-of-truth §6 Product 3) ──────────────────────────
// Shard pricing.
export const RETRY_TOKEN_SHARD_COST = 20;
export const RETRY_TOKEN_BUNDLE_5_SHARD_COST = 90;
export const RETRY_TOKEN_BUNDLE_5_COUNT = 5;

// Daily login grant caps. Base +1/day capped at 3; Keepfall Plus adds the
// headroom to a cap of 5. (The Worker is the authority for these — §6.)
export const RETRY_DAILY_CAP_F2P = 3;
export const RETRY_DAILY_CAP_PLUS = 5;
export const RETRY_DAILY_GRANT_AMOUNT = 1;

// Retry-token rule constants surfaced for clarity in code + errors.
export const RETRY_RULES = {
  CANNOT_RETRY_A_WIN: "cannot_retry_a_win",
  CANNOT_RETRY_A_RETRY: "cannot_retry_a_retry",
  ATTEMPT_NOT_FOUND: "attempt_not_found",
  ATTEMPT_NOT_RESOLVED: "attempt_not_resolved",
  INSUFFICIENT_TOKENS: "insufficient_tokens",
} as const;

/**
 * Read a positive integer from env with a documented fallback. Keeps the Worker
 * resilient if a [vars] entry is missing locally; production sets them all.
 */
export function envInt(value: string | undefined, fallback: number): number {
  if (value == null) return fallback;
  const n = Number.parseInt(value, 10);
  return Number.isFinite(n) ? n : fallback;
}

/** Resolve the Plus product id from env, falling back to the canonical default. */
export function plusProductId(env: Env): string {
  return env.PLUS_PRODUCT_ID || PLUS_PRODUCT_FALLBACK_ID;
}
