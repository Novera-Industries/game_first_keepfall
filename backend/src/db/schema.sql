-- Keepfall Phase 1 — D1 schema (canonical).
-- Scope: accounts + cloud save + receipt/retry authority ONLY. No gameplay simulation.
-- (source-of-truth §11). Every economy constant referenced here traces to §6.
--
-- This file is the human-readable canonical schema. The applied migration is
-- src/db/migrations/0001_init.sql and MUST mirror this file exactly.
--
-- Conventions:
--   *_utc columns are INTEGER unix-epoch MILLISECONDS (UTC). The client never
--   supplies "now"; the Worker stamps it. Booleans are INTEGER 0/1.

PRAGMA foreign_keys = ON;

-- ── accounts ────────────────────────────────────────────────────────────────
-- One row per Apple account. apple_user_id is the stable Sign in with Apple
-- subject; it is the only identity we store (Phase 1 has no email/social graph,
-- see anti-pattern §10.10 — no friend graph).
CREATE TABLE IF NOT EXISTS accounts (
  id              TEXT    PRIMARY KEY,            -- opaque uuid we mint
  apple_user_id   TEXT    NOT NULL UNIQUE,        -- Sign in with Apple subject
  created_utc     INTEGER NOT NULL
);

-- ── cloud_saves ─────────────────────────────────────────────────────────────
-- Opaque client save blob. Last-write-wins by updated_utc (saves.ts). The
-- backend never parses gameplay out of the blob — it only stores/returns it.
-- One current save per account (account_id is unique); history is not kept in
-- Phase 1.
CREATE TABLE IF NOT EXISTS cloud_saves (
  account_id      TEXT    NOT NULL UNIQUE
                    REFERENCES accounts(id) ON DELETE CASCADE,
  blob            TEXT    NOT NULL,               -- opaque (JSON/base64) save state
  schema_version  INTEGER NOT NULL,              -- client save-schema version
  updated_utc     INTEGER NOT NULL,              -- client-asserted save time (LWW key)
  device_id       TEXT    NOT NULL                -- last device that wrote
);

-- ── receipts ────────────────────────────────────────────────────────────────
-- One row per verified StoreKit 2 transaction. transaction_id is unique so
-- consumable grants (e.g. retry tokens, Shard packs) are idempotent — replaying
-- a receipt never double-grants (receipts.ts). type is the product category.
CREATE TABLE IF NOT EXISTS receipts (
  id                        TEXT    PRIMARY KEY,
  account_id                TEXT    NOT NULL
                              REFERENCES accounts(id) ON DELETE CASCADE,
  product_id                TEXT    NOT NULL,
  transaction_id            TEXT    NOT NULL UNIQUE,   -- StoreKit 2 transactionId
  original_transaction_id   TEXT    NOT NULL,          -- subscription lineage key
  type                      TEXT    NOT NULL
                              CHECK (type IN ('consumable', 'subscription', 'nonconsumable')),
  verified_utc              INTEGER NOT NULL,
  raw                       TEXT    NOT NULL            -- decoded JWS payload (audit trail)
);

-- ── entitlements ────────────────────────────────────────────────────────────
-- Current entitlement state per (account, product). For Keepfall Plus
-- (the ONE subscription tier, §6 Product 2) `active` + `period_end_utc` drive
-- whether Plus perks apply. On expiry/cancellation `active` flips to 0, but
-- cosmetics earned during the subscription are KEPT (enforced in receipts.ts +
-- tests) — §6 "Cosmetics earned during a subscription are kept on cancellation".
CREATE TABLE IF NOT EXISTS entitlements (
  account_id      TEXT    NOT NULL
                    REFERENCES accounts(id) ON DELETE CASCADE,
  product_id      TEXT    NOT NULL,
  kind            TEXT    NOT NULL
                    CHECK (kind IN ('subscription', 'nonconsumable')),
  active          INTEGER NOT NULL DEFAULT 0,    -- 0/1
  period_end_utc  INTEGER,                       -- subscription renewal boundary; NULL for nonconsumable
  PRIMARY KEY (account_id, product_id)
);

-- ── match_attempts ──────────────────────────────────────────────────────────
-- One row per PvE attempt. The server records seed + ai_tier so a retry can be
-- recreated with IDENTICAL conditions (retry.ts / §6 Product 3). reward_stone is
-- this attempt's actual payout; first_attempt_reward_stone is the cap the retry
-- inherits ("rewards capped at the first-attempt rate"). is_retry + parent
-- enforce "cannot retry a retry".
CREATE TABLE IF NOT EXISTS match_attempts (
  id                          TEXT    PRIMARY KEY,
  account_id                  TEXT    NOT NULL
                                REFERENCES accounts(id) ON DELETE CASCADE,
  match_seed                  TEXT    NOT NULL,        -- deterministic map/hand seed
  ai_tier                     INTEGER NOT NULL         -- 1..5 (§4 AI difficulty tiers)
                                CHECK (ai_tier BETWEEN 1 AND 5),
  started_utc                 INTEGER NOT NULL,
  result                      TEXT    NOT NULL DEFAULT 'pending'
                                CHECK (result IN ('pending', 'win', 'loss')),
  is_retry                    INTEGER NOT NULL DEFAULT 0,   -- 0/1
  parent_attempt_id           TEXT
                                REFERENCES match_attempts(id) ON DELETE SET NULL,
  reward_stone                INTEGER NOT NULL DEFAULT 0,
  first_attempt_reward_stone  INTEGER NOT NULL DEFAULT 0    -- reward cap inherited by retries
);

-- ── retry_ledger ────────────────────────────────────────────────────────────
-- Append-only ledger of retry-token balance changes. balance_after lets us read
-- the current balance from the latest row without a separate counter, and gives
-- an audit trail. delta>0 grants, delta<0 spends. source records origin.
-- The Worker is the sole authority for this balance (§6 Product 3).
CREATE TABLE IF NOT EXISTS retry_ledger (
  id              TEXT    PRIMARY KEY,
  account_id      TEXT    NOT NULL
                    REFERENCES accounts(id) ON DELETE CASCADE,
  delta           INTEGER NOT NULL,              -- + grant / - spend
  source          TEXT    NOT NULL
                    CHECK (source IN ('login', 'plus', 'pass', 'purchase', 'redeem')),
  balance_after   INTEGER NOT NULL,             -- balance immediately after this row
  created_utc     INTEGER NOT NULL
);

-- ── indexes ─────────────────────────────────────────────────────────────────
CREATE UNIQUE INDEX IF NOT EXISTS idx_accounts_apple_user_id
  ON accounts (apple_user_id);

CREATE INDEX IF NOT EXISTS idx_receipts_account
  ON receipts (account_id);
CREATE INDEX IF NOT EXISTS idx_receipts_original_txn
  ON receipts (original_transaction_id);

CREATE INDEX IF NOT EXISTS idx_entitlements_account
  ON entitlements (account_id);

CREATE INDEX IF NOT EXISTS idx_match_attempts_account
  ON match_attempts (account_id);
CREATE INDEX IF NOT EXISTS idx_match_attempts_parent
  ON match_attempts (parent_attempt_id);

CREATE INDEX IF NOT EXISTS idx_retry_ledger_account_created
  ON retry_ledger (account_id, created_utc);
