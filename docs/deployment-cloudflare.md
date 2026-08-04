# Cloudflare Deployment

This guide describes the production deployment path for the Catalog API.

## Resources

| Resource            | Value                                                                     |
| ------------------- | ------------------------------------------------------------------------- |
| Worker              | `catalog-api`                                                             |
| D1 database         | `catalog-data`                                                            |
| Worker binding      | `DB`                                                                      |
| Migration directory | `migrations`                                                              |
| Public route        | `https://catalog-api.agentflow-sdlc-agentic-commerce-catalog.workers.dev` |

`wrangler.jsonc` is the source of truth for the Worker name, compatibility settings, observability, D1 binding, and migration directory.

## Authentication

Interactive local operations use Wrangler OAuth:

```bash
npx wrangler login
npx wrangler whoami
```

GitHub Actions requires these repository or `production` environment secrets:

- `CLOUDFLARE_API_TOKEN`: a least-privilege token scoped to the deployment account with Workers Scripts and D1 write access.
- `CLOUDFLARE_ACCOUNT_ID`: the Cloudflare account identifier.

Never commit either value. The public deployment URL is stored as the repository variable `CATALOG_BASE_URL` after a successful deployment.

## Local migrations

List and apply migrations against Wrangler's isolated local database:

```bash
npx wrangler d1 migrations list catalog-data --local
npm run db:migrate:local
```

The migrations create `categories` and `products`, including unique SKU and normalized category-name indexes, non-negative price and active-status checks, the optional category foreign key, lookup indexes, and audit timestamps.

## Remote migrations

Inspect pending migrations before applying them:

```bash
npx wrangler d1 migrations list catalog-data --remote
npm run db:migrate:remote
npx wrangler d1 migrations list catalog-data --remote
```

Do not edit a migration after it has been applied remotely. Correct production schema changes with a new forward migration. If a migration fails, preserve the error, confirm Wrangler rolled back the failed migration, inspect the migration list, and fix it with a new versioned file where necessary.

## Manual deployment

Run all gates, migrate D1, and deploy in order:

```bash
npm ci
npm run check
npm run db:migrate:remote
npm run deploy
```

Wrangler reports the deployed public target. Verify it with:

```bash
npm run verify:health -- https://catalog-api.<account-subdomain>.workers.dev
```

The current production deployment is available at:

```text
https://catalog-api.agentflow-sdlc-agentic-commerce-catalog.workers.dev
```

The health verifier makes five bounded attempts and requires HTTP `200` with the expected operational status, service name, and version.

## Continuous deployment

`.github/workflows/deploy-cloudflare.yml` runs after a push to `main` and supports `workflow_dispatch`. It performs:

1. Checkout and Node.js setup.
2. `npm ci`.
3. `npm run check`.
4. Remote D1 migrations.
5. Worker deployment.
6. Structured extraction of the deployed URL from Wrangler output.
7. Public health verification.
8. A GitHub Actions deployment summary.

The job uses minimum `contents: read` GitHub permissions, the `production` environment, and a concurrency group that prevents overlapping production deployments.

## Deployment URL

The authoritative URL is available in three places:

- The successful deployment's GitHub Actions summary.
- Wrangler's structured deployment output.
- The `CATALOG_BASE_URL` GitHub repository variable.

The current value is `https://catalog-api.agentflow-sdlc-agentic-commerce-catalog.workers.dev`.

## Logs

Stream production logs with:

```bash
npx wrangler tail catalog-api
```

The Worker emits structured JSON completion and error records containing correlation IDs.

## Basic rollback

List deployments and versions before rolling back:

```bash
npx wrangler deployments list --name catalog-api
npx wrangler versions list --name catalog-api
```

After identifying the last known-good version, follow the reviewed rollback procedure:

```bash
npx wrangler rollback <VERSION_ID> --name catalog-api
```

A Worker rollback does not roll back D1 state. Database recovery must be assessed independently with D1 backups or Time Travel; never delete or reinitialize the production database as a rollback shortcut.

## Playwright status

Post-deployment Playwright execution and publishing QA artifacts remain outside this change. They belong to the next Agentic SDLC work block and must not be configured in the QA repository yet.
