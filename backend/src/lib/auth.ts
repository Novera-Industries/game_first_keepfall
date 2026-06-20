// Keepfall Phase 1 — bearer session tokens.
//
// Phase 1 uses a simple, self-contained HMAC-signed token (no external IdP, no
// session table). A token is `base64url(payload).base64url(hmacSha256(payload))`
// where payload = {sub: accountId, iat, exp}. The Worker signs on /v1/accounts
// and verifies on every authed route.
//
// Rationale: Phase 1 is single-player, iOS-only, low blast radius. The signing
// key AUTH_HMAC_SECRET lives in wrangler secrets (never committed). When Phase 2
// adds multiplayer/social, swap this for rotating keys or a real session store —
// the verify/issue surface here is intentionally small to make that swap easy.

import { envInt } from "./config";

export interface TokenPayload {
  /** account id (subject). */
  sub: string;
  /** issued-at, epoch seconds. */
  iat: number;
  /** expiry, epoch seconds. */
  exp: number;
}

export type VerifyResult =
  | { ok: true; payload: TokenPayload }
  | { ok: false; reason: "malformed" | "bad_signature" | "expired" | "no_secret" };

/** Issue a signed bearer token for an account. */
export async function issueToken(
  accountId: string,
  secret: string,
  ttlSeconds: number,
  nowSeconds: number = Math.floor(Date.now() / 1000),
): Promise<string> {
  const payload: TokenPayload = {
    sub: accountId,
    iat: nowSeconds,
    exp: nowSeconds + ttlSeconds,
  };
  const payloadB64 = b64urlEncode(JSON.stringify(payload));
  const sig = await hmacSign(payloadB64, secret);
  return `${payloadB64}.${sig}`;
}

/** Verify a bearer token. Constant-time signature compare; checks expiry. */
export async function verifyToken(
  token: string,
  secret: string | undefined,
  nowSeconds: number = Math.floor(Date.now() / 1000),
): Promise<VerifyResult> {
  if (!secret) return { ok: false, reason: "no_secret" };

  const dot = token.indexOf(".");
  if (dot <= 0 || dot === token.length - 1) {
    return { ok: false, reason: "malformed" };
  }
  const payloadB64 = token.slice(0, dot);
  const sig = token.slice(dot + 1);

  const expected = await hmacSign(payloadB64, secret);
  if (!timingSafeEqual(sig, expected)) {
    return { ok: false, reason: "bad_signature" };
  }

  let payload: TokenPayload;
  try {
    payload = JSON.parse(b64urlDecode(payloadB64)) as TokenPayload;
  } catch {
    return { ok: false, reason: "malformed" };
  }
  if (typeof payload.sub !== "string" || typeof payload.exp !== "number") {
    return { ok: false, reason: "malformed" };
  }
  if (payload.exp <= nowSeconds) {
    return { ok: false, reason: "expired" };
  }
  return { ok: true, payload };
}

/**
 * Extract and verify the account id from an Authorization: Bearer header.
 * Returns null on any failure (the route then responds 401).
 */
export async function authedAccountId(
  authHeader: string | null,
  secret: string | undefined,
): Promise<string | null> {
  if (!authHeader) return null;
  const m = /^Bearer\s+(.+)$/i.exec(authHeader.trim());
  if (!m) return null;
  const result = await verifyToken(m[1], secret);
  return result.ok ? result.payload.sub : null;
}

/** Read the session TTL (seconds) from env, default 7 days. */
export function sessionTtlSeconds(env: { SESSION_TTL_SECONDS?: string }): number {
  return envInt(env.SESSION_TTL_SECONDS, 604800);
}

// ── crypto helpers (Web Crypto, available in workerd) ────────────────────────

async function hmacSign(data: string, secret: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sigBuf = await crypto.subtle.sign(
    "HMAC",
    key,
    new TextEncoder().encode(data),
  );
  return b64urlEncodeBytes(new Uint8Array(sigBuf));
}

/** Constant-time string compare to avoid signature timing leaks. */
function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) {
    diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return diff === 0;
}

function b64urlEncode(s: string): string {
  return b64urlEncodeBytes(new TextEncoder().encode(s));
}

function b64urlEncodeBytes(bytes: Uint8Array): string {
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function b64urlDecode(b64url: string): string {
  const b64 = b64url.replace(/-/g, "+").replace(/_/g, "/");
  const pad = b64.length % 4 === 0 ? "" : "=".repeat(4 - (b64.length % 4));
  const binary = atob(b64 + pad);
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}
