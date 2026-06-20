// Keepfall Phase 1 — test bootstrap: apply D1 migrations into the test database.
//
// The vitest-pool-workers runtime gives each test file an isolated D1 instance
// bound as env.DB, but it starts empty. This setup module (run via
// setupFiles in vitest.config.ts) reads src/db/migrations and applies them so
// every suite runs against the real schema.

import { applyD1Migrations, env } from "cloudflare:test";

// TEST_MIGRATIONS is injected by vitest.config.ts (readD1Migrations at config
// time, surfaced through miniflare bindings). It is the parsed migration list.
declare module "cloudflare:test" {
  interface ProvidedEnv {
    TEST_MIGRATIONS: D1Migration[];
    // Mirror wrangler.toml [vars] used by the tests.
    DB: D1Database;
    ENVIRONMENT: string;
    APP_BUNDLE_ID: string;
    PLUS_PRODUCT_ID: string;
    RETRY_TOKEN_SHARD_COST: string;
    RETRY_TOKEN_BUNDLE_5_SHARD_COST: string;
    RETRY_DAILY_CAP_F2P: string;
    RETRY_DAILY_CAP_PLUS: string;
    SESSION_TTL_SECONDS: string;
    AUTH_HMAC_SECRET: string;
  }
}

await applyD1Migrations(env.DB, env.TEST_MIGRATIONS);
