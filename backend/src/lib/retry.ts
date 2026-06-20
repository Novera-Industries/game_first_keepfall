// Keepfall Phase 1 — RETRY-TOKEN AUTHORITY (pure logic).
//
// THE WORKER IS THE AUTHORITY. The client is never trusted with these rules.
// (source-of-truth §6 Product 3.)
//
// Rules enforced here:
//   1. Cannot retry a win.
//   2. Cannot retry a retry.
//   3. Rewards on a retry are capped at the FIRST-attempt rate.
//   4. The token balance can never go negative (underflow guard).
//   5. Daily grant: +1 base (cap 3); +1 more headroom if Plus active (cap 5);
//      never double-grant on the same UTC day.
//
// Everything in this file is PURE — no DB, no clock, no env. The I/O wrapper in
// index.ts feeds these functions snapshot data and persists their decisions
// atomically. This is what the test suite hammers.

import {
  RETRY_DAILY_CAP_F2P,
  RETRY_DAILY_CAP_PLUS,
  RETRY_DAILY_GRANT_AMOUNT,
  RETRY_RULES,
} from "./config";

/** Minimal view of a match attempt needed to decide a redeem. */
export interface AttemptSnapshot {
  id: string;
  result: "pending" | "win" | "loss";
  isRetry: boolean;
  /** Reward cap a retry inherits — the original first attempt's payout rate. */
  firstAttemptRewardStone: number;
  /** Conditions the retry must reproduce identically. */
  matchSeed: string;
  aiTier: number;
}

export type RedeemDenialReason =
  (typeof RETRY_RULES)[keyof typeof RETRY_RULES];

export type CanRedeemResult =
  | { ok: true }
  | { ok: false; reason: RedeemDenialReason };

/**
 * canRedeem — the gate for spending a retry token on an attempt.
 *
 * Returns { ok:true } only when ALL hold:
 *   - the attempt exists and is resolved (loss; pending/win are rejected),
 *   - the attempt is NOT itself a retry,
 *   - the player holds at least one token.
 *
 * Order matters: we report the most specific rule violation first so the client
 * can show honest copy ("You cannot retry a win.") rather than a generic error.
 */
export function canRedeem(
  attempt: AttemptSnapshot | null,
  tokenBalance: number,
): CanRedeemResult {
  if (attempt == null) {
    return { ok: false, reason: RETRY_RULES.ATTEMPT_NOT_FOUND };
  }
  if (attempt.result === "win") {
    return { ok: false, reason: RETRY_RULES.CANNOT_RETRY_A_WIN };
  }
  if (attempt.result === "pending") {
    // A match still in progress has no resolved outcome to retry.
    return { ok: false, reason: RETRY_RULES.ATTEMPT_NOT_RESOLVED };
  }
  if (attempt.isRetry) {
    return { ok: false, reason: RETRY_RULES.CANNOT_RETRY_A_RETRY };
  }
  if (tokenBalance < 1) {
    return { ok: false, reason: RETRY_RULES.INSUFFICIENT_TOKENS };
  }
  return { ok: true };
}

/** Spec for the retry attempt row to be created when a redeem succeeds. */
export interface RetryAttemptSpec {
  /** Identical conditions copied from the parent (§6: "identical AI, seed, hand"). */
  matchSeed: string;
  aiTier: number;
  parentAttemptId: string;
  isRetry: true;
  /** Reward this retry may pay out is capped at the first-attempt rate. */
  rewardCapStone: number;
}

export interface ApplyRedeemResult {
  /** New token balance after spending one. Guaranteed >= 0. */
  newBalance: number;
  /** Ledger delta to persist (always -1 here). */
  ledgerDelta: number;
  /** The retry attempt to create with identical conditions + capped reward. */
  retryAttempt: RetryAttemptSpec;
}

/**
 * applyRedeem — pure state transition for a validated redeem.
 *
 * Precondition: canRedeem(attempt, tokenBalance).ok === true. Callers MUST gate
 * on canRedeem first; applyRedeem re-asserts the invariants and throws if they
 * are violated, so a bug upstream can never silently mint a free retry or drive
 * the balance negative.
 */
export function applyRedeem(
  attempt: AttemptSnapshot,
  tokenBalance: number,
): ApplyRedeemResult {
  const gate = canRedeem(attempt, tokenBalance);
  if (!gate.ok) {
    throw new Error(`applyRedeem called on an invalid redeem: ${gate.reason}`);
  }
  // Underflow guard: we already know balance >= 1 from canRedeem, but assert it.
  const newBalance = tokenBalance - 1;
  if (newBalance < 0) {
    throw new Error("retry token underflow");
  }

  return {
    newBalance,
    ledgerDelta: -1,
    retryAttempt: {
      matchSeed: attempt.matchSeed,
      aiTier: attempt.aiTier,
      parentAttemptId: attempt.id,
      isRetry: true,
      // Rewards capped at the first-attempt rate (§6 Product 3).
      rewardCapStone: attempt.firstAttemptRewardStone,
    },
  };
}

/**
 * cappedRetryReward — clamp a claimed reward to the first-attempt cap.
 *
 * The retry attempt may report a raw reward (e.g. from the client's match
 * result); the authority NEVER pays more than the original first attempt earned,
 * and never less than zero.
 */
export function cappedRetryReward(
  claimedReward: number,
  firstAttemptRewardStone: number,
): number {
  const cap = Math.max(0, firstAttemptRewardStone);
  const claimed = Math.max(0, claimedReward);
  return Math.min(claimed, cap);
}

/**
 * dailyGrantAmount — how many retry tokens a daily-login grant should add.
 *
 * Rules (§6 Product 3):
 *   - Base grant is +1/day, balance capped at 3 for F2P.
 *   - Keepfall Plus raises the cap to 5 (still +1/day).
 *   - If a grant already happened this UTC day, grant 0 (idempotent).
 *   - Never push the balance over its cap, and never return a negative amount
 *     (if the player is already at/over cap, grant 0).
 *
 * Returns the delta to apply (0..1). Pure: the caller decides "today" and whether
 * a grant already happened.
 */
export function dailyGrantAmount(
  currentBalance: number,
  isPlus: boolean,
  alreadyGrantedToday: boolean,
): number {
  if (alreadyGrantedToday) return 0;

  const cap = isPlus ? RETRY_DAILY_CAP_PLUS : RETRY_DAILY_CAP_F2P;
  const headroom = cap - currentBalance;
  if (headroom <= 0) return 0;

  // Grant the base amount, but never exceed the cap.
  return Math.min(RETRY_DAILY_GRANT_AMOUNT, headroom);
}

/**
 * utcDayKey — the UTC calendar day (YYYY-MM-DD) for a timestamp in ms.
 * Used to enforce one daily grant per UTC day. Pure given the input ms.
 */
export function utcDayKey(epochMs: number): string {
  return new Date(epochMs).toISOString().slice(0, 10);
}
