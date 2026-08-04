# Agentic Commerce Catalog

`agentic-commerce-catalog` is the Catalog service bootstrap for the Agentic SDLC MVP. It exposes a small product and category API on Cloudflare Workers, persists data in Cloudflare D1, and keeps business rules isolated from platform concerns.

## Current MVP status

The repository currently provides:

- Product creation, listing, retrieval, activation, and deactivation.
- Category creation and listing.
- Optional product-to-category association.
- Duplicate SKU and normalized category-name protection.
- Consistent error envelopes and request correlation IDs.
- D1 migrations, local integration tests, an OpenAPI contract, and pull-request CI.
- Reproducible Cloudflare deployment automation with remote migrations and a public health check.

Agents, Cloudflare Workflows, search, inventory, orders, authentication, and external knowledge integrations remain outside this bootstrap.

## Architecture

The code uses explicit dependency boundaries:

```text
src/
├── domain/          # Entities and business invariants; no platform dependencies
├── application/     # Use cases and repository/runtime ports
├── infrastructure/  # Cloudflare runtime and D1 adapters
├── api/             # Hono routes, validation, and HTTP translation
└── index.ts          # Worker entry point
```

The domain layer does not import Hono, Cloudflare APIs, or D1. The application layer depends only on domain types and its own ports. `npm run validate:architecture` enforces these boundaries.

Machine-readable repository, architecture, and quality-gate metadata is stored in [`.agentic/`](.agentic/).

## Requirements

- Node.js 22.12 or newer (the repository includes `.nvmrc`).
- npm 10.
- A Cloudflare account is needed only to create a remote D1 database or deploy the Worker.

Local tests use an isolated Miniflare D1 database and do not require Cloudflare credentials.

## Install

```bash
npm ci
```

Generate or verify Worker binding types:

```bash
npm run types:check
```

## Local development

Apply the versioned migrations to Wrangler's local D1 database:

```bash
npm run db:migrate:local
```

Start the local Worker:

```bash
npm run dev
```

The default local URL is `http://localhost:8787`. Check it with:

```bash
curl http://localhost:8787/health
```

## D1 configuration

The Worker expects one D1 binding named `DB`. The binding and migration directory are declared in `wrangler.jsonc`.

The production resource is `catalog-data`. Remote migrations and deployments require authenticated Wrangler access. See [`docs/deployment-cloudflare.md`](docs/deployment-cloudflare.md) for local, manual, and continuous deployment procedures, required GitHub secrets, health verification, logs, and rollback guidance.

The deployed Catalog API is available at `https://catalog-api.agentflow-sdlc-agentic-commerce-catalog.workers.dev`.

## API

| Method  | Path                    | Purpose                           |
| ------- | ----------------------- | --------------------------------- |
| `GET`   | `/health`               | Report service health.            |
| `POST`  | `/products`             | Create a product.                 |
| `GET`   | `/products`             | List products.                    |
| `GET`   | `/products/{id}`        | Retrieve a product.               |
| `PATCH` | `/products/{id}/status` | Activate or deactivate a product. |
| `POST`  | `/categories`           | Create a category.                |
| `GET`   | `/categories`           | List categories.                  |

The complete OpenAPI 3.1 contract, including request/response schemas and error examples, is in [`openapi/catalog-api.yaml`](openapi/catalog-api.yaml).

All responses include an `X-Correlation-ID` header and the same value in the JSON envelope. A valid caller-provided ID is preserved; otherwise the Worker creates one.

## Domain rules

- SKU and product name are required.
- SKUs are trimmed, normalized to uppercase, and unique.
- Price must be finite and greater than or equal to zero.
- New products are active by default and can be deactivated or reactivated.
- Category name is required and unique after trimming, collapsing whitespace, and lowercasing.
- Category association is optional, but a supplied category must exist.

Database constraints reinforce the uniqueness, price, status, and foreign-key rules.

## Commands

| Command                         | Purpose                                               |
| ------------------------------- | ----------------------------------------------------- |
| `npm run dev`                   | Start Wrangler locally.                               |
| `npm run typecheck`             | Verify generated Worker types and strict TypeScript.  |
| `npm run lint`                  | Run ESLint with zero warnings.                        |
| `npm run format:check`          | Check Prettier formatting.                            |
| `npm run validate:architecture` | Enforce core dependency boundaries.                   |
| `npm run validate:openapi`      | Lint the OpenAPI contract.                            |
| `npm run test:unit`             | Run domain unit tests.                                |
| `npm run test:integration`      | Run the Worker against isolated local D1.             |
| `npm test`                      | Run all tests.                                        |
| `npm run check`                 | Run every pull-request quality gate.                  |
| `npm run db:migrate:local`      | Apply Catalog migrations to isolated local D1.        |
| `npm run db:migrate:remote`     | Apply pending migrations to production D1.            |
| `npm run deploy`                | Deploy `catalog-api` with the local Wrangler version. |
| `npm run verify:health`         | Verify the configured public Catalog URL.             |

## Tests and CI

Vitest unit tests cover domain invariants. Worker integration tests apply the real SQL migrations to an isolated local D1 instance and invoke the exported Worker's `fetch` handler. Pull requests run:

```bash
npm ci
npm run check
```

## Repository structure

```text
.agentic/             Agent-readable repository metadata
.github/workflows/    Pull-request CI
migrations/           Ordered D1 schema migrations
openapi/              HTTP API contract
scripts/              Local validation utilities
src/                  Worker implementation
tests/unit/           Domain tests
tests/integration/    Worker and local D1 tests
docs/                 Operational and deployment guides
```
