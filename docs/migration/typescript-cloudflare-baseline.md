# TypeScript and Cloudflare Baseline

## Archive reference

The last stable proof of concept is preserved by the annotated tag `archive/typescript-cloudflare-poc` at commit `b1b8be9b7e62e23f0f0e219454c6020b224783de`. The tag includes both merged Cloudflare pull requests and is the source of truth when porting behavior that is intentionally absent from the .NET foundation.

Before migration, `npm run check` passed formatting, lint, strict TypeScript, architecture validation, OpenAPI validation, and 22 tests across four test files.

## Previous stack

- Node.js 22 and TypeScript 5.9.
- Hono with Zod request validation.
- Cloudflare Workers as the HTTP runtime.
- Cloudflare D1 and Wrangler migrations for persistence.
- Vitest with the Cloudflare Workers pool and isolated Miniflare D1.
- ESLint, Prettier, Redocly, Wrangler, and GitHub Actions.

Cloudflare is a historical implementation baseline, not the current architecture.

## Previous HTTP behavior

| Method | Path | Behavior to port later |
| --- | --- | --- |
| `GET` | `/health` | Returned service status, name, version, and correlation ID. |
| `POST` | `/products` | Created an active product and optionally associated a category. |
| `GET` | `/products` | Listed products newest first. |
| `GET` | `/products/{id}` | Retrieved one product or returned `PRODUCT_NOT_FOUND`. |
| `PATCH` | `/products/{id}/status` | Activated or deactivated a product. |
| `POST` | `/categories` | Created a normalized unique category. |
| `GET` | `/categories` | Listed categories by normalized name. |

Successful Product and Category responses used `{ data, correlationId }` envelopes. Errors used `{ error: { code, message, details }, correlationId }`. The API returned and propagated `X-Correlation-ID`; only values matching `^[A-Za-z0-9._:-]{1,128}$` were accepted.

The .NET foundation deliberately changes only the health payload required for this phase: it is now the direct `{ status, service, version }` contract with status `healthy`. Product and Category contracts remain preserved but pending in OpenAPI.

## Product rules

- SKU and name are required after trimming.
- SKU is normalized to uppercase and must be unique.
- Price must be finite and greater than or equal to zero.
- Description and category association are optional and trimmed.
- New products are active by default.
- Products may be deactivated and reactivated, updating `updatedAt`.
- A supplied category must exist.
- Product IDs used the `PRODUCT-<uuid>` format.

## Category rules

- Name is required after trimming.
- Repeated whitespace is collapsed for display and normalization.
- The normalized name is lowercase and unique.
- Description is optional and trimmed.
- Category IDs used the `CATEGORY-<uuid>` format.

## D1 model

The archived migrations are copied under `docs/migration/d1/` for exact reference.

`categories` contained text IDs, display and normalized names, optional description, and created/updated timestamps. A unique index enforced `normalized_name`.

`products` contained text IDs, unique SKU, name, optional description, non-negative real price, optional category ID, constrained active flag, and timestamps. The category foreign key used `ON DELETE SET NULL`; indexes covered unique SKU and category lookup.

## Previous tests

- Six Product unit tests covered normalization, required values, non-negative price, and status transitions.
- Three Category unit tests covered creation, required names, and normalization.
- Eleven Worker integration tests covered health, correlation, CRUD-oriented Product/Category behavior, conflicts, validation, not-found behavior, and association.
- Two contract tests compared Hono routes with OpenAPI operations and required a successful response for each operation.

## Previous scripts and automation

- `npm run check` composed formatting, lint, type checking, architecture, OpenAPI, and tests.
- Wrangler commands handled local/remote D1 migrations, development, deployment, and health verification.
- GitHub Actions validated pull requests and deployed `main` to Cloudflare after applying D1 migrations.
- The historical deployment was `https://catalog-api.agentflow-sdlc-agentic-commerce-catalog.workers.dev`.

## Compatibility requirements for future slices

Future Product and Category slices must review the archived tag and preserve routes, request and response shapes, normalization rules, error codes/statuses, ordering, optional association, uniqueness behavior, and timestamps unless a separately reviewed contract change is approved.

## Removed from the active architecture

Hono, Workers, D1 bindings, Wrangler, Miniflare, Vitest, npm tooling, Cloudflare deployment automation, and TypeScript are not active after this migration. Azure is the future execution platform, Azure DevOps administers the SDLC, Azure Pipelines provides validation, and GitHub remains the repository and pull-request system.
