// Keepfall Phase 1 — retry-token AUTHORITY tests.
//
// These exercise the pure rules in src/lib/retry.ts. They are the contract for
// the most sensitive logic in the backend (source-of-truth §6 Product 3):
//   - cannot retry a win
//   - cannot retry a retry
//   - rewards capped at the first-attempt rate
//   - daily grant idempotency (one per UTC day)
//   - cap 3 (F2P) / cap 5 (Plus)
//   - decrement underflow guard

import { describe, it, expect } from "vitest";
import {
  canRedeem,
  applyRedeem,
  cappedRetryReward,
  dailyGrantAmount,
  utcDayKey,
  type AttemptSnapshot,
} from "../src/lib/retry";

const lossAttempt: AttemptSnapshot = {
  id: "att-1",
  result: "loss",
  isRetry: false,
  firstAttemptRewardStone: 100,
  matchSeed: "seed-abc",
  aiTier: 3,
};

describe("canRedeem — the authority gate", () => {
  it("allows a retry on a resolved loss when the player holds a token", () => {
    expect(canRedeem(lossAttempt, 1)).toEqual({ ok: true });
  });

  it("CANNOT retry a win", () => {
    const win: AttemptSnapshot = { ...lossAttempt, result: "win" };
    expect(canRedeem(win, 5)).toEqual({
      ok: false,
      reason: "cannot_retry_a_win",
    });
  });

  it("CANNOT retry a retry", () => {
    const retry: AttemptSnapshot = { ...lossAttempt, isRetry: true };
    expect(canRedeem(retry, 5)).toEqual({
      ok: false,
      reason: "cannot_retry_a_retry",
    });
  });

  it("reports win before retry-of-retry when both would apply (most specific first)", () => {
    const wonRetry: AttemptSnapshot = { ...lossAttempt, result: "win", isRetry: true };
    expect(canRedeem(wonRetry, 5)).toEqual({
      ok: false,
      reason: "cannot_retry_a_win",
    });
  });

  it("rejects a pending (unresolved) attempt", () => {
    const pending: AttemptSnapshot = { ...lossAttempt, result: "pending" };
    expect(canRedeem(pending, 5)).toEqual({
      ok: false,
      reason: "attempt_not_resolved",
    });
  });

  it("rejects a missing attempt", () => {
    expect(canRedeem(null, 5)).toEqual({
      ok: false,
      reason: "attempt_not_found",
    });
  });

  it("rejects when the player has no tokens", () => {
    expect(canRedeem(lossAttempt, 0)).toEqual({
      ok: false,
      reason: "insufficient_tokens",
    });
  });
});

describe("applyRedeem — state transition + reward cap", () => {
  it("decrements the balance by exactly one", () => {
    const r = applyRedeem(lossAttempt, 3);
    expect(r.newBalance).toBe(2);
    expect(r.ledgerDelta).toBe(-1);
  });

  it("creates an identical retry attempt (same seed + ai tier + parent)", () => {
    const r = applyRedeem(lossAttempt, 1);
    expect(r.retryAttempt.matchSeed).toBe("seed-abc");
    expect(r.retryAttempt.aiTier).toBe(3);
    expect(r.retryAttempt.parentAttemptId).toBe("att-1");
    expect(r.retryAttempt.isRetry).toBe(true);
  });

  it("stamps the reward cap = first-attempt reward", () => {
    const r = applyRedeem(lossAttempt, 1);
    expect(r.retryAttempt.rewardCapStone).toBe(100);
  });

  it("throws if called on an invalid redeem (defensive — caller must gate)", () => {
    const win: AttemptSnapshot = { ...lossAttempt, result: "win" };
    expect(() => applyRedeem(win, 5)).toThrow(/invalid redeem/);
  });

  it("underflow guard: never produces a negative balance", () => {
    // canRedeem would reject balance 0, and applyRedeem re-asserts it.
    expect(() => applyRedeem(lossAttempt, 0)).toThrow(/insufficient_tokens/);
  });
});

describe("cappedRetryReward — rewards capped at first-attempt rate", () => {
  it("clamps a higher claimed reward down to the first-attempt cap", () => {
    expect(cappedRetryReward(250, 100)).toBe(100);
  });

  it("allows a claimed reward at or below the cap", () => {
    expect(cappedRetryReward(80, 100)).toBe(80);
    expect(cappedRetryReward(100, 100)).toBe(100);
  });

  it("never returns a negative reward", () => {
    expect(cappedRetryReward(-50, 100)).toBe(0);
    expect(cappedRetryReward(50, -10)).toBe(0);
  });
});

describe("dailyGrantAmount — daily login grant rules", () => {
  it("grants +1 for an F2P player below cap", () => {
    expect(dailyGrantAmount(0, false, false)).toBe(1);
    expect(dailyGrantAmount(2, false, false)).toBe(1);
  });

  it("F2P cap is 3 — no grant at or above the cap", () => {
    expect(dailyGrantAmount(3, false, false)).toBe(0);
    expect(dailyGrantAmount(4, false, false)).toBe(0);
  });

  it("Plus cap is 5 — grants up to the higher cap", () => {
    expect(dailyGrantAmount(3, true, false)).toBe(1); // F2P would be 0 here
    expect(dailyGrantAmount(4, true, false)).toBe(1);
  });

  it("Plus cap is 5 — no grant at or above 5", () => {
    expect(dailyGrantAmount(5, true, false)).toBe(0);
    expect(dailyGrantAmount(6, true, false)).toBe(0);
  });

  it("idempotency: never grants twice in the same UTC day", () => {
    expect(dailyGrantAmount(0, false, true)).toBe(0);
    expect(dailyGrantAmount(0, true, true)).toBe(0);
  });

  it("never grants past the cap even from below (clamped to headroom)", () => {
    // F2P at 2 has headroom 1; grant is min(1, 1) = 1, landing exactly on cap 3.
    expect(dailyGrantAmount(2, false, false)).toBe(1);
  });
});

describe("utcDayKey — UTC day bucketing for idempotency", () => {
  it("buckets timestamps by UTC calendar day", () => {
    const a = Date.parse("2026-06-20T00:00:00.000Z");
    const b = Date.parse("2026-06-20T23:59:59.999Z");
    const c = Date.parse("2026-06-21T00:00:00.000Z");
    expect(utcDayKey(a)).toBe("2026-06-20");
    expect(utcDayKey(b)).toBe("2026-06-20");
    expect(utcDayKey(c)).toBe("2026-06-21");
    expect(utcDayKey(a)).toBe(utcDayKey(b));
    expect(utcDayKey(a)).not.toBe(utcDayKey(c));
  });
});
