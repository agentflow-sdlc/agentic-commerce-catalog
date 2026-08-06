# Agentic Commerce Catalog


`agentic-commerce-catalog` is the Catalog service for the Agentic SDLC MVP. Its basic Product and Category behavior now has functional parity in .NET 10, ASP.NET Core, EF Core, and SQL Server-compatible persistence.

Azure DevOps administers Boards, Pipelines, states, and evidence. GitHub stores the repository, branches, commits, and pull requests.

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

Product status changes use only `{ "isActive": true|false }`. The operation preserves SKU, name, price, description, and category. Matching the historical baseline, every valid status request writes the requested state and a new `updatedAt`, including a repeated state.

Category creation generates `CATEGORY-<guid>`, trims and collapses repeated whitespace for the display name, and compares the lowercase normalized name for uniqueness. The historical optional description and both timestamps are preserved. The internal normalized name is not exposed by the public response. Categories are listed by normalized name and ID.

## IDesign structure

```text
HTTP -> Catalog.Api -> Catalog.Managers -> Catalog.Engines
                                      -> domain-owned accessor ports
                                      -> Catalog.Accessors -> EF Core -> SQL Server
```

| Project | Responsibility |
| --- | --- |
| `Catalog.Contracts` | Provider-neutral Product, Category, collection, status, health, and error contracts. |
| `Catalog.Api` | HTTP translation, correlation, safe logging, middleware, composition, and OpenAPI. |
| `Catalog.Managers` | Product and Category use-case coordination. |
| `Catalog.Engines` | Deterministic Product/Category rules, domain models, and accessor ports. |
| `Catalog.Accessors` | Technical access to information containers; this implementation uses EF Core and SQL Server. |

The API references Accessors only in the composition root. Managers use domain-owned ports and never reference EF Core, SQL, or Accessor implementations. Future file, API, object-store, or other information-container access also belongs in `Catalog.Accessors`.

See [system design](docs/idesign/system-design.md), [project design](docs/idesign/project-design.md), [use cases](docs/idesign/use-cases.md), and [volatility analysis](docs/idesign/volatility-analysis.md).

## Requirements

- .NET SDK `10.0.302`, pinned by `global.json`.
- Access to NuGet.org during restore.
- SQL Server or an Azure SQL-compatible connection for the application.
- Docker for the isolated SQL Server integration suite.

Restore the repository-local EF Core tool with `dotnet tool restore`.

## Database configuration

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

Development OpenAPI is available at `/openapi/v1.json`. The technology-neutral source contract is [`openapi/catalog-api.yaml`](openapi/catalog-api.yaml).

## Errors, correlation, and logging

Errors use `{ error: { code, message, details }, correlationId }`. Supported functional codes include:

- `PRODUCT_VALIDATION_FAILED`
- `PRODUCT_SKU_ALREADY_EXISTS`
- `PRODUCT_NOT_FOUND`
- `CATEGORY_VALIDATION_FAILED`
- `CATEGORY_NAME_ALREADY_EXISTS`
- `CATEGORY_NOT_FOUND`
- `UNEXPECTED_ERROR`

Expected errors return safe details and the correlation ID. Logs cover use-case starts, lists, Product status, Product–Category association, duplicate names/SKUs, not-found results, and unexpected failures. Request bodies, SQL, connection strings, stack traces, and provider exception messages are not returned or functionally logged.

## EF Core migrations

`src/Catalog.Accessors/Migrations` contains:

- `InitialProduct`, which creates Products and the unique SKU constraint.
- `AddCategoriesAndProductCategory`, which creates Categories, the unique normalized-name index, nullable Product `CategoryId`, its index, and an `ON DELETE SET NULL` foreign key without recreating Products.

Create a migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj \
  --output-dir Migrations
```

Apply all migrations:

```bash
dotnet ef database update \
  --project src/Catalog.Accessors/Catalog.Accessors.csproj \
  --startup-project src/Catalog.Api/Catalog.Api.csproj
```

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
```

The default test command reports container tests as skipped. Run the real isolated SQL Server suite on a Docker host:

```bash
RUN_SQL_SERVER_TESTS=true dotnet test \
  tests/Catalog.Product.IntegrationTests/Catalog.Product.IntegrationTests.csproj \
  --configuration Release
```

The fixture starts an ephemeral SQL Server, applies `InitialProduct` first and then the full migration chain, resets Product and Category data between cases, verifies real unique indexes and the foreign key, and removes the container afterward. It never uses EF Core InMemory or a shared Azure database.

## Azure Pipeline

`azure-pipelines.yml` restores tools/packages, builds Release, runs unit/API/architecture/OpenAPI tests, requires Docker for the clean-database SQL suite, validates EF migrations and formatting, publishes TRX evidence, and retains diagnostics on failure. It contains no deployment, service connection, infrastructure, or remote Azure SQL configuration.

## Current limitations

Basic Product and Category parity is implemented. Product/Category deletion or general editing, search, pagination, inventory, orders, authentication, authorization, distributed events, caching, Azure deployment, infrastructure, external Microsoft.Playwright verification, and agents remain out of scope.

## Repository structure

```text
.agentic/                                   Machine-readable architecture and quality metadata
.config/dotnet-tools.json                   Pinned dotnet-ef tool
src/Catalog.Contracts/                      Public HTTP contracts
src/Catalog.Api/                            ASP.NET Core endpoints and middleware
src/Catalog.Managers/                       Product and Category coordination
src/Catalog.Engines/                        Product/Category rules, models, and ports
src/Catalog.Accessors/Sql/                   EF Core SQL Server Accessors
src/Catalog.Accessors/Migrations/            Incremental EF Core migrations
tests/Catalog.Api.Tests/                     In-memory HTTP and OpenAPI tests
tests/Catalog.Product.Tests/                 Product and Category unit tests
tests/Catalog.Product.IntegrationTests/      Isolated SQL Server end-to-end tests
tests/Catalog.Architecture.Tests/            Executable dependency rules
openapi/                                    Technology-neutral HTTP contract
docs/idesign/                               Implemented IDesign evidence
docs/migration/                             Historical migration evidence
azure-pipelines.yml                         Pull-request and main validation
```

## Previous baseline

The TypeScript, Hono, Cloudflare Workers, and D1 proof of concept is preserved only as historical evidence at tag `archive/typescript-cloudflare-poc` and under `docs/migration/`. None of those technologies is an active runtime dependency.
