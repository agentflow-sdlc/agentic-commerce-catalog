# Catalog System Design

## Components

- **Client — `Catalog.Api`:** HTTP translation, correlation, safe logs, middleware, composition, health, OpenAPI, and optional telemetry.
- **Managers — `Catalog.Managers`:** Product and Category use-case coordination, time/ID acquisition, and mapping.
- **Engines — `Catalog.Engines`:** deterministic models, rules, stable ID validation, and provider-neutral Accessor ports.
- **Accessors — `Catalog.Accessors`:** EF Core mapping and SQL Server access; future information-container implementations share this boundary.
- **Contracts — `Catalog.Contracts`:** provider-neutral public requests, responses, collection envelopes, and errors.
- **Database Migrator — `Catalog.DatabaseMigrator`:** one-shot migration host; depends on Accessors and exposes no HTTP endpoints.
- **Infrastructure — `Catalog.Infrastructure`:** Pulumi Azure Native composition outside every production dependency chain.

## Application flows

```text
HTTP -> API -> Manager -> Engine/domain port -> SQL Accessor -> SQL Server
```

Product validation runs before Accessor checks. Within valid creation, SKU existence is checked before Category existence to preserve baseline behavior. Product Engine never queries Category persistence.

## Dependency direction

```text
Catalog.Api -> Catalog.Contracts / Catalog.Managers / Catalog.Accessors (composition only)
Catalog.Managers -> Catalog.Engines
Catalog.Accessors -> Catalog.Engines
Catalog.DatabaseMigrator -> Catalog.Accessors

Production projects -X-> Catalog.Infrastructure / Pulumi
Catalog.Api -X-> EF migration APIs / Catalog.DatabaseMigrator
Catalog.DatabaseMigrator -X-> ASP.NET Core / HTTP endpoints
Catalog.Engines and Catalog.Managers -X-> Azure / EF Core / SQL implementations
```

Executable architecture tests inspect assembly references and endpoint IL. Endpoint delegates cannot call Engines, Accessors, EF Core, or SQL directly.

## Persistence and concurrency

Incremental EF Core migrations create Product and Category storage, unique indexes, and the nullable Product-Category foreign key. SQL Server remains authoritative for write races. Collection reads use one `AsNoTracking` query each and return only fields required by the public contract.

## Azure dev topology

```text
Internet --HTTPS--> Azure Container Apps ingress --> Catalog API
                                                      |
Container Apps environment --> delegated VNet subnet  |
                                                      v
SQL private DNS --> SQL Private Endpoint --> Azure SQL

ACR --managed identity/AcrPull--> API and migrator images
Key Vault --managed identity/Secrets User--> CatalogDb reference
API --managed identity/Metrics Publisher--> Application Insights --> Log Analytics
```

The SQL server rejects public network traffic. The API never applies migrations. The migration job shares the private network and secure references, runs to completion, and exposes no ingress.

## Delivery topology

GitHub owns the code, the review gate, and the execution of the SDLC. `.github/workflows/ci-cd.yml` is the only CI/CD definition — there is no Azure Pipelines definition in this repository and no second deployment pipeline. Azure DevOps Boards remains the work-item system only.

```text
GitHub Pull Request -> main
  GitHub Actions --> validate --> infrastructure-preview --> publish-evidence
                        |                |
                        |                +-- pulumi preview (read-only, existing dev stack)
                        +-- build, unit/API/architecture/integration tests,
                            OpenAPI validation, EF model check, format
  No image build. No pulumi up. No Azure mutation.

GitHub main
  GitHub Actions --> validate --> infrastructure-preview --> build-images --> deploy
                          --> run-migrations --> smoke-tests --> publish-evidence
                                  |                 |               |
      ACR <-- az acr build -------+                 |               +-- artifacts + run manifest
      Azure Container Apps <-- pulumi up            |
      Azure SQL <-- Container Apps migrator job ----+
```

The two flows share one workflow. Deployment jobs are gated on the event not being a pull request and the ref being `refs/heads/main`, so review happens in GitHub while mutation happens only after integration. A `concurrency` group serialises runs against the shared `dev` stack.

Every `pulumi up` is preceded by a `pulumi preview` whose digest is retained as evidence and inspected for destructive operations. A plan that would delete or replace Azure SQL, ACR, Key Vault, the managed identity, Log Analytics, Application Insights, the Container Apps environment, the container app, or the resource group fails the job before the update runs. The workflow never calls `pulumi destroy`, reuses the existing backend and `dev` stack, and keeps the existing passphrase secrets provider, so repeated runs converge rather than duplicate.

Images carry immutable `<run-id>-<short-sha>` tags and the ACR tags are locked after the build; `latest` is never deployed. Azure access uses OIDC workload identity federation with `id-token: write` and no other elevated permission, so no client secret, PAT, or storage key exists in the delivery path.

## Error and observability decision

Managers translate controlled domain failures into stable HTTP errors, and global middleware maps unexpected failures to a safe correlated 500. Structured logs exclude request bodies, SQL values, secrets, and provider details. Azure Monitor registration is conditional, so the API still starts locally when Application Insights is absent.
