# Agentic Commerce Catalog

`agentic-commerce-catalog` is the Catalog service for the Agentic SDLC MVP. Product and Category behavior runs on .NET 10, ASP.NET Core, EF Core, and Azure SQL. Pulumi C# provisions an isolated Azure dev environment, while Azure Container Apps hosts the API and the one-shot database migrator.

GitHub stores the repository, branches, commits, and pull requests, and runs CI/CD through GitHub Actions. Azure DevOps Boards remains the work-item and state system; it no longer runs pipelines for this repository.

## Available endpoints

```http
GET   /health
POST  /products
GET   /products
GET   /products/{id}
PATCH /products/{id}/status
POST  /categories
GET   /categories
```

Successful Catalog responses use the historical `{ data, correlationId }` envelope, except for the direct health response retained by the .NET foundation. Every response includes `X-Correlation-ID`.

## Functional behavior

Product creation normalizes SKU and text, accepts non-negative `decimal(18,2)` prices, creates an active `PRODUCT-<guid>`, and optionally stores one `categoryId`. The Manager rejects unknown categories; the database foreign key remains authoritative during write races. Products are listed by `createdAt DESC, id DESC` without pagination or per-row queries.

Product status changes use only `{ "isActive": true|false }`. Every valid status request writes the requested state and a new `updatedAt`, including a repeated state.

Category creation generates `CATEGORY-<guid>`, normalizes its display and comparison names, and preserves the optional description and timestamps. Categories are listed by normalized name and ID.

## IDesign structure

```text
HTTP -> Catalog.Api -> Catalog.Managers -> Catalog.Engines
                                      -> domain-owned accessor ports
                                      -> Catalog.Accessors -> EF Core -> SQL Server
```

| Project | Responsibility |
| --- | --- |
| `Catalog.Contracts` | Provider-neutral Product, Category, health, collection, status, and error contracts. |
| `Catalog.Api` | HTTP translation, correlation, safe logging, middleware, composition, OpenAPI, and optional telemetry. |
| `Catalog.Managers` | Product and Category use-case coordination. |
| `Catalog.Engines` | Deterministic rules, domain models, and accessor ports. |
| `Catalog.Accessors` | Technical access to information containers; this implementation uses EF Core and SQL Server. |
| `Catalog.DatabaseMigrator` | One-shot EF Core migration executable with no HTTP surface. |
| `Catalog.Infrastructure` | Pulumi Azure Native components; no production project references it. |

The API references Accessors only in the composition root. Managers use domain-owned ports and never reference EF Core, SQL, or Accessor implementations. Future file, API, object-store, or other information-container access also belongs in `Catalog.Accessors`.

See [system design](docs/idesign/system-design.md), [project design](docs/idesign/project-design.md), [use cases](docs/idesign/use-cases.md), and [volatility analysis](docs/idesign/volatility-analysis.md).

## Requirements

- .NET SDK `10.0.302`, pinned by `global.json`.
- Access to NuGet.org during restore.
- SQL Server or an Azure SQL-compatible connection for the application.
- Docker for the isolated SQL Server integration suite.
- PowerShell 7, Azure CLI, and Pulumi CLI for Azure deployment.
- An active Azure CLI subscription with permission to create the documented dev resources.

Restore the repository-local EF Core tool with `dotnet tool restore`.

## Local database configuration

The connection string is named `CatalogDb`. No credential is committed.

```bash
ConnectionStrings__CatalogDb="Server=localhost,1433;Database=AgenticCommerceCatalog;User Id=sa;Password=<local-password>;TrustServerCertificate=True" \
  dotnet run --project src/Catalog.Api/Catalog.Api.csproj
```

For local user secrets:

```bash
dotnet user-secrets init --project src/Catalog.Api/Catalog.Api.csproj
dotnet user-secrets set "ConnectionStrings:CatalogDb" "<connection-string>" \
  --project src/Catalog.Api/Catalog.Api.csproj
```

## Run locally

```bash
dotnet restore Catalog.sln
dotnet run --project src/Catalog.Api/Catalog.Api.csproj
```

Development OpenAPI is available at `/openapi/v1.json`. The provider-neutral source contract is [`openapi/catalog-api.yaml`](openapi/catalog-api.yaml).

## Errors, correlation, and logging

Errors use `{ error: { code, message, details }, correlationId }`. Expected errors return safe details and the correlation ID. Structured logs contain request metadata, stable IDs, counts, and state; they exclude request bodies, SQL values, passwords, connection strings, tokens, provider exception details, and functional stack traces.

Application Insights registration is optional. Local execution remains functional when `ApplicationInsights:ConnectionString` is absent. Azure uses the workload managed identity to authenticate telemetry ingestion.

## EF Core migrations

`src/Catalog.Accessors/Migrations` contains the incremental Product and Category schema history. Create a migration with:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj \
  --output-dir Migrations
```

Production does not run migrations in the API process. Azure executes `Catalog.DatabaseMigrator` through a manually triggered Container Apps Job before smoke tests.

## Validation

```bash
dotnet tool restore
dotnet restore Catalog.sln
dotnet build Catalog.sln --configuration Release --no-restore
dotnet test Catalog.sln --configuration Release --no-build
dotnet format Catalog.sln --verify-no-changes --no-restore
dotnet ef migrations has-pending-model-changes \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj \
  --configuration Release \
  --no-build
dotnet build infra/Catalog.Infrastructure/Catalog.Infrastructure.csproj --configuration Release
```

The default test command reports container tests as skipped. Run the isolated SQL Server suite on a Docker host:

```bash
RUN_SQL_SERVER_TESTS=true dotnet test \
  tests/Catalog.Product.IntegrationTests/Catalog.Product.IntegrationTests.csproj \
  --configuration Release
```

## Azure dev deployment

The deployment reuses the private Azure Blob Pulumi backend in `rg-agentic-pulumi-state`, its `pulumi-state` container, stack `dev`, and the passphrase secrets provider. It does not create or migrate the backend. `Pulumi.dev.yaml` may be committed only with an `encryptionsalt` and a `secure:` value for `catalog:sqlAdminPassword`.

From an authenticated Azure CLI session, with the Pulumi passphrase and SQL admin password supplied by a secure process environment:

```powershell
pwsh ./scripts/deploy-dev.ps1
```

The script validates the repository, provisions the foundation, builds immutable commit-based API and migrator images through ACR Tasks, deploys the workloads, runs the migration job, and verifies `/health`, `/products`, and `/categories` from inside the Container Apps Environment. Smoke evidence is written to `artifacts/deployment/dev/<commit-sha>/` and ignored by Git.

Its ACR Tasks step currently fails on this subscription (`TasksOperationsNotAllowed`); see [Container image builds](#container-image-builds). CI is unaffected.

```text
GitHub -> Pulumi C# -> ACR -> Azure Container Apps -> Catalog API -> Azure SQL
                                |                       |
                                +-> migration job       +-> Application Insights
```

### Network boundary

**The Catalog API is internal-only in the Azure dev/POC environment and is not reachable from the Internet.** Its Container Apps ingress is `external: false`, so it resolves and answers only inside the Container Apps Environment.

This matters because the API implements **no application-layer authentication** and exposes mutating operations (`POST /products`, `PATCH /products/{id}/status`, `POST /categories`). HTTPS protects the transport; it does not make an unauthenticated API private. The private network boundary is currently the only access control, and it is described here as exactly that rather than as production-grade zero-trust security.

Consequences worth knowing:

- CI cannot reach the API from a GitHub-hosted runner, and must not be changed so it can. Post-deployment smoke runs *inside* the environment through a manual Container Apps Job started over the Azure management plane — see [`scripts/internal-smoke-dev.ps1`](scripts/internal-smoke-dev.ps1).
- The `catalogUrl` stack output is an environment-internal FQDN. It is not a public address and cannot be opened from a laptop.
- Developer access from a local machine will be provided separately through authorized private connectivity. That work is not part of this repository yet, so there is currently no supported way to call the deployed API by hand.
- `scripts/tests/preview-guard-check.ps1` fails the build if a Pulumi preview would set external ingress, so this boundary cannot be removed unnoticed.

Azure SQL is a separate resource with its own boundary: public access is disabled and Container Apps reaches it through a delegated VNet subnet, SQL Private Endpoint, and private DNS. A user-assigned managed identity provides ACR pulls, Key Vault secret references, and Azure Monitor ingestion.

SQL authentication is a temporary MVP exception. The password exists only in encrypted Pulumi configuration and in the Key Vault connection-string secret. It never enters images, source files, logs, PR text, outputs, or smoke evidence.

See [`infra/Catalog.Infrastructure/README.md`](infra/Catalog.Infrastructure/README.md) for rollback, security, cost, backend, and Azure DevOps setup details.

## CI/CD

GitHub owns the code and runs the SDLC. [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml) is the single CI/CD definition; there is no Azure Pipelines definition in this repository and no second deployment pipeline.

| | |
| --- | --- |
| Workflow | `ci-cd` |
| Runner | `ubuntu-latest` |
| Azure authentication | OIDC / workload identity federation via `azure/login` |
| Repository variables | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`, `PULUMI_BACKEND_URL`, `PULUMI_STACK`, `CATALOG_SQL_ADMIN_LOGIN` |
| Repository secrets | `PULUMI_CONFIG_PASSPHRASE`, `CATALOG_SQL_ADMIN_PASSWORD` |

### Pull request into `main` — validation only, never deploys

```text
GitHub Pull Request -> GitHub Actions
  validate                restore, build, unit/API/architecture/integration tests,
                          OpenAPI validation, EF migration model, dotnet format
  infrastructure-preview  pulumi preview against the existing dev stack
  publish-evidence        test results + preview digest as artifacts
```

The deployment jobs are gated on `github.event_name != 'pull_request'` **and** `github.ref == 'refs/heads/main'`, so a pull request can never build images, run `pulumi up`, migrate the database, or mutate Azure.

### Push to `main` — full deployment

```text
GitHub main -> GitHub Actions
  validate                identical quality gates
  infrastructure-preview  pulumi preview against the existing dev stack
  build-images            docker build + push -> catalog-api and catalog-database-migrator,
                          immutable tag <run-id>-<short-sha>, never `latest`
  deploy                  pulumi preview (destruction gate) -> pulumi up --yes
  run-migrations          start the Container Apps migrator job and await its result
  smoke-tests             GET /health, /products, /categories (inside the environment)
  publish-evidence        consolidated artifacts + run manifest
```

A `concurrency` group keeps two runs from mutating the shared `dev` stack at once: pull request runs supersede each other, while `main` deployments queue.

### Container image builds

Images live in **GitHub Container Registry**, not Azure Container Registry. ACR Basic costs a fixed monthly amount whether or not anything pulls from it, and this repository is public, so its container packages can be public too without exposing anything new.

Public GHCR packages are pulled **anonymously**, which means Azure Container Apps needs no registry credential at all — no personal access token, no secret in Key Vault, nothing to rotate. `GITHUB_TOKEN` pushes during the workflow and expires with the job.

`build-images` builds on the runner's Docker daemon, pushes, makes the packages public, and then verifies an anonymous pull actually succeeds. That verification is what protects the ACR retirement: `deploy` is the job that removes ACR, so a failure at any earlier point leaves it in place.

`az acr build` (ACR Tasks) is not used at all — this subscription refuses ACR Tasks with `TasksOperationsNotAllowed`, and the registry is being retired anyway. `scripts/deploy-dev.ps1` still calls it and will hit that wall if run locally.

### Cost profile

The stack runs under a free-first profile. See [`docs/azure-cost-optimization.md`](docs/azure-cost-optimization.md) for the audit and [`docs/azure-cost-baseline.md`](docs/azure-cost-baseline.md) for the resulting baseline.

| Setting | Default | Effect |
| --- | --- | --- |
| `catalog:costProfile` | `poc-free` | Selects the free or minimum option wherever a choice exists |
| `catalog:allowPaidResources` | `false` | Anything off the free path must opt in explicitly |
| `catalog:sqlUseFreeOffer` | `false` | Separate opt-in — adopting the SQL free offer **replaces and destroys** the database |

Under `poc-free` the stack does not provision ACR, private endpoints, dedicated Container Apps compute, a production SQL SKU, premium Functions plans, premium Cosmos, a paid Azure AI Search tier, or provisioned model capacity.

### Stack configuration is committed

`pulumi config set` writes to `infra/Catalog.Infrastructure/Pulumi.dev.yaml`, a file in the working tree — a DIY backend stores state, not configuration. A CI checkout is therefore whatever is committed, and anything the `deploy` job sets is discarded when the runner is destroyed.

`catalog:deployWorkload` must consequently stay `true` in the committed file once the workload exists. If it were left `false`, the next `pulumi preview` would compute a plan that deletes the Container App and the migrator job, and the destruction guard would correctly fail every pull request.

`catalog:apiImage` and `catalog:migratorImage` are committed for the same reason. **Known limitation:** `deploy` overrides them with the freshly built tag but does not commit the result, so after a deployment the committed references lag behind what is running and a pull request preview shows a benign image update. This is never destructive and the guard passes. To remove the lag, either have `deploy` commit the updated file back to `main`, or move image selection out of Pulumi configuration.

### Infrastructure safety

Every `pulumi up` is preceded by `pulumi preview` through [`scripts/invoke-pulumi-preview.ps1`](scripts/invoke-pulumi-preview.ps1). The preview digest is stored as evidence and the job **fails before any update** when the plan would delete or replace Azure SQL, ACR, Key Vault, the managed identity, Log Analytics, Application Insights, the Container Apps environment, the container app, or the resource group. `pulumi destroy` is never invoked by the workflow. The guard has an offline self-check, [`scripts/tests/preview-guard-check.ps1`](scripts/tests/preview-guard-check.ps1), which runs as part of `validate`.

The workflow reuses the existing backend, the existing `dev` stack, and the existing passphrase secrets provider. It creates no second registry, database, vault, or container app, so repeated runs on the same commit converge on the same resources.

### Secrets

`PULUMI_CONFIG_PASSPHRASE` and `CATALOG_SQL_ADMIN_PASSWORD` exist only as GitHub repository **secrets**. They are referenced by name in the workflow and never written to the repository, the logs, the artifacts, or the run manifest. Azure authentication uses OIDC workload identity federation — there is no client secret, no PAT, and no storage account key in the deployment path. `permissions` is `contents: read` plus `id-token: write` and nothing else.

## Current limitations

Product and Category parity plus the Azure dev deployment are implemented. Product/Category deletion or general editing, search, pagination, inventory, orders, authentication, authorization, distributed events, caching, production infrastructure, external Microsoft.Playwright verification, and agents remain out of scope.

## Repository structure

```text
.agentic/                                   Machine-readable architecture and quality metadata
.github/workflows/ci-cd.yml                 The only CI/CD definition (GitHub Actions)
.config/dotnet-tools.json                   Pinned dotnet-ef tool
src/Catalog.Contracts/                      Public HTTP contracts
src/Catalog.Api/                            ASP.NET Core endpoints and middleware
src/Catalog.Managers/                       Product and Category coordination
src/Catalog.Engines/                        Product/Category rules, models, and ports
src/Catalog.Accessors/Sql/                   EF Core SQL Server Accessors
src/Catalog.Accessors/Migrations/            Incremental EF Core migrations
src/Catalog.DatabaseMigrator/                Non-HTTP migration executable and image
infra/Catalog.Infrastructure/                Pulumi Azure Native dev infrastructure
scripts/                                     Idempotent deployment and verification scripts
scripts/tests/                               Offline self-checks for the deployment scripts
tests/                                       Unit, API, SQL integration, and architecture tests
openapi/                                     Provider-neutral HTTP contract
docs/idesign/                                Implemented IDesign evidence
docs/migration/                              Historical migration evidence
```

## Previous baseline

The TypeScript, Hono, Cloudflare Workers, and D1 proof of concept is preserved only as historical evidence at tag `archive/typescript-cloudflare-poc` and under `docs/migration/`. None of those technologies is an active runtime dependency.
