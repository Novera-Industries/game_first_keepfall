-- Migration 0001 — initial Keepfall Phase 1 schema.
-- MIRRORS src/db/schema.sql exactly. Keep the two in lockstep on every change.
-- Applied via `npm run db:migrate` (wrangler d1 migrations apply DB).

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS accounts (
  id              TEXT    PRIMARY KEY,
  apple_user_id   TEXT    NOT NULL UNIQUE,
  created_utc     INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS cloud_saves (
  account_id      TEXT    NOT NULL UNIQUE
                    REFERENCES accounts(id) ON DELETE CASCADE,
  blob            TEXT    NOT NULL,
  schema_version  INTEGER NOT NULL,
  updated_utc     INTEGER NOT NULL,
  device_id       TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS receipts (
  id                        TEXT    PRIMARY KEY,
  account_id                TEXT    NOT NULL
                              REFERENCES accounts(id) ON DELETE CASCADE,
  product_id                TEXT    NOT NULL,
  transaction_id            TEXT    NOT NULL UNIQUE,
  original_transaction_id   TEXT    NOT NULL,
  type                      TEXT    NOT NULL
                              CHECK (type IN ('consumable', 'subscription', 'nonconsumable')),
  verified_utc              INTEGER NOT NULL,
  raw                       TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS entitlements (
  account_id      TEXT    NOT NULL
                    REFERENCES accounts(id) ON DELETE CASCADE,
  product_id      TEXT    NOT NULL,
  kind            TEXT    NOT NULL
                    CHECK (kind IN ('subscription', 'nonconsumable')),
  active          INTEGER NOT NULL DEFAULT 0,
  period_end_utc  INTEGER,
  PRIMARY KEY (account_id, product_id)
);

CREATE TABLE IF NOT EXISTS match_attempts (
  id                          TEXT    PRIMARY KEY,
  account_id                  TEXT    NOT NULL
                                REFERENCES accounts(id) ON DELETE CASCADE,
  match_seed                  TEXT    NOT NULL,
  ai_tier                     INTEGER NOT NULL
                                CHECK (ai_tier BETWEEN 1 AND 5),
  started_utc                 INTEGER NOT NULL,
  result                      TEXT    NOT NULL DEFAULT 'pending'
                                CHECK (result IN ('pending', 'win', 'loss')),
  is_retry                    INTEGER NOT NULL DEFAULT 0,
  parent_attempt_id           TEXT
                                REFERENCES match_attempts(id) ON DELETE SET NULL,
  reward_stone                INTEGER NOT NULL DEFAULT 0,
  first_attempt_reward_stone  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS retry_ledger (
  id              TEXT    PRIMARY KEY,
  account_id      TEXT    NOT NULL
                    REFERENCES accounts(id) ON DELETE CASCADE,
  delta           INTEGER NOT NULL,
  source          TEXT    NOT NULL
                    CHECK (source IN ('login', 'plus', 'pass', 'purchase', 'redeem')),
  balance_after   INTEGER NOT NULL,
  created_utc     INTEGER NOT NULL
);

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
