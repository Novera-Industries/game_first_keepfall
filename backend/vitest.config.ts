import {
  defineWorkersConfig,
  readD1Migrations,
} from "@cloudflare/vitest-pool-workers/config";
import path from "node:path";

// Read the D1 migrations at config time so they can be applied per test file
// (see test/apply-migrations.ts). Run the suite inside the workerd runtime so
// D1, crypto.subtle, and the Workers globals behave exactly as in production.
export default defineWorkersConfig(async () => {
  const migrations = await readD1Migrations(
    path.join(__dirname, "src/db/migrations"),
  );

  return {
    test: {
      globals: true,
      // Apply migrations into the isolated D1 before every test file.
      setupFiles: ["./test/apply-migrations.ts"],
      poolOptions: {
        workers: {
          wrangler: { configPath: "./wrangler.toml" },
          miniflare: {
            compatibilityFlags: ["nodejs_compat"],
            // Surface the parsed migrations to the test runtime as a binding.
            bindings: {
              TEST_MIGRATIONS: migrations,
              // Auth secret is a wrangler secret in prod; supply a test value so
              // token issue/verify works in the suite.
              AUTH_HMAC_SECRET: "test-hmac-secret-keepfall",
            },
          },
        },
      },
    },
  };
});
