# Agentic Commerce Catalog

`agentic-commerce-catalog` is the Catalog service for the Agentic SDLC MVP. It provides a provider-neutral Product domain and a .NET 10 vertical slice backed by EF Core and SQL Server-compatible persistence.

Azure DevOps administers Boards, Pipelines, states, and evidence. GitHub stores the repository, branches, commits, and pull requests.

## Current status

The service implements these endpoints:

```http
GET /health
POST /products
GET /products/{id}
```

Product creation normalizes SKU and text, rejects invalid or duplicate SKUs, accepts non-negative `decimal(18,2)` prices including zero, generates a stable `PRODUCT-<guid>` application ID, starts products as active, and assigns deterministic timestamps through `TimeProvider`. Product retrieval validates that stable ID format before persistence and returns a stable not-found error for unknown canonical IDs.

Product listing, status changes, Category, search, filtering, inventory, orders, authentication, deployment, Azure infrastructure, external Playwright, agents, and orchestration are outside this slice.

## Architecture

The implemented request path is:

```text
HTTP -> Catalog.Api -> Catalog.Managers -> Catalog.Engines
                                      -> domain IProductAccessor port
                                      -> Catalog.Accessors -> EF Core -> SQL Server
```

| Project | Responsibility |
| --- | --- |
| `Catalog.Contracts` | Provider-neutral HTTP request and response contracts. |
| `Catalog.Api` | ASP.NET Core entry point, composition, HTTP translation, middleware, health, and development OpenAPI. |
| `Catalog.Managers` | Create and get-by-ID use-case coordination. |
| `Catalog.Engines` | Deterministic Product rules, Product model, and provider-neutral accessor port. |
| `Catalog.Accessors` | Concrete access to information containers. This slice implements the EF Core SQL Server accessor; future file, API, or object-store accessors also belong here. |

The API references `Catalog.Accessors` only in the composition root. Managers depend on the domain port and never on EF Core or the SQL implementation.

See the [system design](docs/idesign/system-design.md), [project design](docs/idesign/project-design.md), [use cases](docs/idesign/use-cases.md), and [volatility analysis](docs/idesign/volatility-analysis.md).

## Requirements

- .NET SDK `10.0.302`, pinned by `global.json`.
- Access to NuGet.org during restore.
- SQL Server or Azure SQL-compatible connection for Product requests.
- Docker only when running the ephemeral SQL Server integration suite.

Restore the repository-local EF Core tool before migration commands:

```bash
dotnet tool restore
```

## Database configuration

The connection string name is `CatalogDb`. No secret is committed. Development configuration contains a Windows LocalDB connection without credentials.

Use an environment variable in any environment:

```bash
ConnectionStrings__CatalogDb="Server=localhost,1433;Database=AgenticCommerceCatalog;User Id=sa;Password=<local-password>;TrustServerCertificate=True" dotnet run --project src/Catalog.Api/Catalog.Api.csproj
```

For local user secrets:

```bash
dotnet user-secrets init --project src/Catalog.Api/Catalog.Api.csproj
dotnet user-secrets set "ConnectionStrings:CatalogDb" "<connection-string>" --project src/Catalog.Api/Catalog.Api.csproj
```

Azure SQL uses the same `CatalogDb` connection-string boundary. Environment-specific credentials must be supplied by the runtime secret mechanism, never by `appsettings` files.

## Run locally

```bash
dotnet restore Catalog.sln
dotnet run --project src/Catalog.Api/Catalog.Api.csproj
```

Use the URL printed by ASP.NET Core. Every response receives an `X-Correlation-ID`; a valid caller-provided value is propagated. Expected Product failures are translated to stable 400, 404, or 409 responses, and unexpected exceptions return a safe 500 response without implementation details.

Successful Product responses preserve the previous `{ data, correlationId }` envelope. Errors use `{ error: { code, message, details }, correlationId }`. Functional validation returns `PRODUCT_VALIDATION_FAILED` with empty public `details`; its internal reason code is recorded only in safe logs. Malformed JSON returns `REQUEST_VALIDATION_FAILED`. Logs record use-case start, successful creation/retrieval, validation, SKU conflicts, not-found results, and unexpected failures without recording request bodies, SQL, connection strings, or stack traces.

Runtime OpenAPI is available at `/openapi/v1.json` in Development.

## Migrations

The initial Product migration is under `src/Catalog.Accessors/Migrations` and creates only the `Products` table, including:

- application-assigned `nvarchar(128)` primary key;
- required unique `nvarchar(64)` SKU;
- required `nvarchar(200)` name;
- nullable `nvarchar(2000)` description;
- `decimal(18,2)` price with a non-negative check constraint;
- active state and `datetimeoffset` timestamps.

Create a future migration with:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj \
  --output-dir Migrations
```

Apply committed migrations to the configured `CatalogDb` database with:

```bash
dotnet ef database update \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj
```

Verify that the model and committed migration agree:

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj
```

## Validate the repository

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
```

The default test command runs all non-container tests and reports the SQL integration tests as skipped. On a Docker-enabled host, run the real SQL Server suite with:

```bash
RUN_SQL_SERVER_TESTS=true dotnet test \
  tests/Catalog.Product.IntegrationTests/Catalog.Product.IntegrationTests.csproj \
  --configuration Release
```

The SQL suite starts an ephemeral SQL Server container, applies the committed EF migration, verifies health, exercises POST and GET through the complete HTTP stack, checks invalid fields and IDs, validates camelCase and correlation behavior, verifies duplicate-SKU behavior at both use-case and database levels, proves persistence between requests, and removes the container after the suite.

## Public OpenAPI contract

[`openapi/catalog-api.yaml`](openapi/catalog-api.yaml) is the technology-neutral public contract. It marks health, Product creation, and Product retrieval as implemented; later Product and Category operations remain explicitly pending.

## Azure Pipelines

`azure-pipelines.yml` validates pull requests and `main`. It restores tools and packages, builds Release, runs all tests with the Docker-backed SQL suite enabled, publishes TRX evidence, verifies formatting, validates OpenAPI, and verifies that EF Core has no pending model changes. It contains no deployment or infrastructure configuration.

Product list and status, Category, Azure deployment, external Playwright, and all agent runtimes remain pending.

## Repository structure

```text
.agentic/                                   Machine-readable repository and architecture metadata
.config/dotnet-tools.json                   Pinned dotnet-ef tool
src/Catalog.Contracts/                      Public HTTP contracts
src/Catalog.Api/                            ASP.NET Core endpoints and middleware
src/Catalog.Managers/                       Product use-case coordination
src/Catalog.Engines/                        Product rules, domain model, and neutral ports
src/Catalog.Accessors/Sql/                   EF Core SQL Server implementation
src/Catalog.Accessors/Migrations/            Reproducible Product migration
tests/Catalog.Api.Tests/                     In-memory HTTP and OpenAPI tests
tests/Catalog.Product.Tests/                 Engine and Manager unit tests
tests/Catalog.Product.IntegrationTests/      Ephemeral SQL Server end-to-end tests
tests/Catalog.Architecture.Tests/            Executable dependency rules
openapi/                                    Technology-neutral HTTP contract
docs/idesign/                               IDesign documentation
docs/migration/                             Historical Cloudflare/D1 evidence
azure-pipelines.yml                         Pull-request and main validation
```

## Previous baseline

The TypeScript, Hono, Cloudflare Workers, and D1 proof of concept is preserved at tag `archive/typescript-cloudflare-poc`. Its behavior and compatibility evidence remain under `docs/migration/`; D1 files are historical evidence and are not active migrations.
