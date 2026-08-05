# Agentic Commerce Catalog

`agentic-commerce-catalog` is the Catalog service for the Agentic SDLC MVP. This phase establishes its .NET 10, ASP.NET Core, and IDesign foundation while preserving the prior Product and Category contract for later vertical slices.

Azure DevOps administers Boards, Pipelines, states, and evidence. GitHub stores the repository, branches, commits, and pull requests. Azure is the future execution platform; this repository does not deploy Azure resources yet.

## Current status

Only one functional endpoint is implemented in .NET:

```http
GET /health
```

```json
{
  "status": "healthy",
  "service": "catalog-api",
  "version": "0.1.0"
}
```

Product and Category operations remain documented in OpenAPI and will be ported in the next vertical slice. There is no persistence, database provider, Azure infrastructure, authentication, agent, or orchestration runtime.

## Architecture

| Project | Current responsibility |
| --- | --- |
| `Catalog.Contracts` | Stable HTTP contracts required by implemented behavior. |
| `Catalog.Api` | ASP.NET Core entry point, HTTP translation, middleware, health, and development OpenAPI. |
| `Catalog.Managers` | Future use-case coordination boundary; no use cases yet. |
| `Catalog.Engines` | Future deterministic Product and Category rules; no rules yet. |
| `Catalog.Accessors` | Future access to SQL, files, APIs, object stores, and other information containers; no accessors yet. |

Managers, Engines, and Accessors contain only assembly markers needed to execute architecture tests. They do not contain placeholder services, repositories, or provider abstractions.

See the concrete [system design](docs/idesign/system-design.md), [project design](docs/idesign/project-design.md), [use cases](docs/idesign/use-cases.md), and [volatility analysis](docs/idesign/volatility-analysis.md).

## Requirements

- .NET SDK `10.0.302`, pinned by `global.json`.
- Access to NuGet.org during restore.

## Validate the repository

```bash
dotnet restore Catalog.sln
dotnet build Catalog.sln --configuration Release --no-restore
dotnet test Catalog.sln --configuration Release --no-build
dotnet format Catalog.sln --verify-no-changes --no-restore
```

Validate the preserved OpenAPI source explicitly:

```bash
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj \
  --configuration Release \
  --no-build \
  --filter Category=OpenApi
```

Package versions are centralized in `Directory.Packages.props`. Nullable references, implicit usings, analyzers, deterministic builds, and warnings-as-errors are configured in `Directory.Build.props`.

## Run locally

```bash
dotnet run --project src/Catalog.Api/Catalog.Api.csproj
```

Use the URL printed by ASP.NET Core, then request `/health`. Every response receives an `X-Correlation-ID`; a valid caller-provided value is propagated. Unexpected exceptions are logged and translated to a stable response without stack traces.

ASP.NET Core generates runtime OpenAPI at `/openapi/v1.json` in the Development environment. It reflects only implemented runtime endpoints.

## Public OpenAPI contract

[`openapi/catalog-api.yaml`](openapi/catalog-api.yaml) remains the technology-neutral public contract. It marks `/health` as `implemented` and all Product and Category operations as `pending-dotnet`. The pending routes, requests, responses, and domain schemas remain available for the next slices.

OpenAPI syntax is validated with the Microsoft OpenAPI.NET YAML reader in `Catalog.Api.Tests` and in Azure Pipelines.

## Tests

`Catalog.Api.Tests` uses `WebApplicationFactory<Program>` and ASP.NET Core TestServer; no external process or database is required. It verifies health JSON, service metadata, correlation generation and propagation, 404 behavior, development OpenAPI, safe unexpected errors, and source OpenAPI validity.

`Catalog.Architecture.Tests` loads every production assembly and enforces the IDesign dependency boundaries, including the rule that Engines cannot use Accessors and the API cannot bypass Managers to reach information containers.

## Azure Pipelines

`azure-pipelines.yml` validates pull requests and changes to `main`. It installs the SDK from `global.json`, restores, builds Release, runs tests, publishes TRX results, verifies formatting, validates OpenAPI, and publishes diagnostic test artifacts on failure. It contains no deployment, service connection, infrastructure, or secret configuration.

## Previous baseline

The TypeScript, Hono, Cloudflare Workers, and D1 proof of concept is preserved at tag `archive/typescript-cloudflare-poc`. Its behavior, rules, schema, tests, scripts, and compatibility requirements are recorded in [`docs/migration/typescript-cloudflare-baseline.md`](docs/migration/typescript-cloudflare-baseline.md). Historical D1 migrations are retained under `docs/migration/d1/` as evidence, not active persistence.

## Repository structure

```text
.agentic/                         Machine-readable repository and architecture metadata
src/Catalog.Contracts/           Stable shared contracts
src/Catalog.Api/                 ASP.NET Core entry point and middleware
src/Catalog.Managers/            Future use-case coordination boundary
src/Catalog.Engines/             Future business-rule boundary
src/Catalog.Accessors/           Future information-container access boundary
tests/Catalog.Api.Tests/         In-memory HTTP, middleware, and OpenAPI tests
tests/Catalog.Architecture.Tests/ Executable dependency rules
openapi/                          Preserved public HTTP contract
docs/idesign/                     Concrete IDesign documentation
docs/migration/                   Historical baseline and D1 evidence
azure-pipelines.yml               Pull-request and main validation
```

## Migration phases

1. TypeScript/Cloudflare Catalog proof of concept — archived.
2. .NET 10 Catalog and IDesign foundation — this phase.
3. Implement the Product vertical slice — next.
4. Port Category behavior and approved persistence/infrastructure in separately reviewed work.

## Out of scope

Product, Category, EF Core, SQL, Azure SQL, D1, Pulumi, Container Apps, Functions, Key Vault, remote Application Insights, Microsoft Agent Framework, Durable Task, agentic workflows, Azure DevOps or GitHub integrations, Playwright, authentication, search, inventory, and orders are not implemented in this phase.
