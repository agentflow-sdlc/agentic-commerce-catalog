# Catalog Clean Architecture

The Catalog follows Clean Architecture (STD-ARC-001): references point inward only, from the clients through infrastructure and application to the domain. `tests/Catalog.Architecture.Tests` enforces the rule on every production assembly and inspects endpoint IL.

## Layers and projects

| Layer | Project | Responsibility | May reference |
| --- | --- | --- | --- |
| Domain | `src/Domain` (`Catalog.Domain`) | Product and Category models, deterministic rules (`ProductRules`, `CategoryRules`), stable ID validation, domain exceptions | .NET runtime only |
| Application | `src/Application` (`Catalog.Application`) | Use cases, repository ports (`IProductRepository`, `ICategoryRepository`), ID generators, time, request errors | Domain |
| Infrastructure | `src/Infrastructure/Sql` (`Catalog.Infrastructure.Sql`) | EF Core SQL Server repositories, mapping, constraints, migrations | Application, Domain, EF Core, SQL provider |
| Clients | `src/Clients/Api` (`Catalog.Clients.Api`) | HTTP contracts, endpoints, middleware, correlation, safe logs, composition, OpenAPI, optional telemetry | Application; Infrastructure only to register it |
| Clients | `src/Clients/DatabaseMigrator` (`Catalog.Clients.DatabaseMigrator`) | One-shot EF Core migration process with no HTTP surface | Infrastructure, generic host |
| Delivery | `infra/Catalog.Infrastructure` | Pulumi Azure Native dev resources | Pulumi; no production project references it |

```text
HTTP -> Catalog.Clients.Api -> use case (Catalog.Application) -> ProductRules / CategoryRules (Catalog.Domain)
                                        |
                                        +-> IProductRepository / ICategoryRepository
                                              implemented by Catalog.Infrastructure.Sql -> EF Core -> SQL Server
```

Prohibited: the domain and application never reference ASP.NET Core, EF Core, SQL or Azure; the API never calls the domain, the repositories or EF Core from an endpoint, and never applies migrations; the migrator never exposes HTTP; no production project references Pulumi.

## Use cases

Each use case is one class named verb and object with one `ExecuteAsync`.

| Use case | Flow |
| --- | --- |
| `CreateProduct` | Rules validate all input before any repository call; SKU uniqueness is checked, then an explicit `categoryId` through `ICategoryRepository`; the SQL unique index and foreign key stay authoritative during races. The API returns `201`, the product envelope, `Location` and the correlation id. |
| `GetProduct` | Rules normalize the id before the repository is queried; an unknown id is `PRODUCT_NOT_FOUND`. |
| `ListProducts` | One no-tracking query ordered by `createdAt DESC, id DESC`; an empty catalog returns `data: []`. |
| `SetProductStatus` | Loads the product; the rules return a copy with only the requested state and a new `updatedAt` (a repeated state still refreshes it); one targeted SQL update. |
| `CreateCategory` | Rules build the display and normalized names; the normalized name is pre-checked and the SQL unique index handles races. |
| `ListCategories` | Ordered by normalized name, then id; the normalized name never enters the public contract. |

Health is served by the API itself with service metadata and the correlation header.

## Persistence and concurrency

Incremental EF Core migrations create Product and Category storage, unique indexes, and the nullable Product-Category foreign key. SQL Server remains authoritative for write races. Collection reads use one `AsNoTracking` query each and return only fields required by the public contract.

## Errors and observability

Use cases translate controlled domain failures into request errors that the API maps to stable HTTP errors; global middleware maps unexpected failures to a safe correlated 500. Structured logs exclude request bodies, SQL values, secrets, and provider details. Azure Monitor registration is conditional, so the API still starts locally when Application Insights is absent.

## Azure dev topology

```text
Container Apps environment --internal ingress--> Catalog API
                                                      |
Container Apps environment --> delegated VNet subnet  |
                                                      v
SQL private DNS --> SQL Private Endpoint --> Azure SQL

ACR --managed identity/AcrPull--> API and migrator images
Key Vault --managed identity/Secrets User--> CatalogDb reference
API --managed identity/Metrics Publisher--> Application Insights --> Log Analytics
```

The Catalog API is internal-only: its ingress is `external: false`, so it answers only inside the Container Apps environment. The SQL server rejects public network traffic. The API never applies migrations. The migration job shares the private network and secure references, runs to completion, and exposes no ingress.

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
      ACR <-- docker build/push --+                 |               +-- artifacts + run manifest
      Azure Container Apps <-- pulumi up            |
      Azure SQL <-- Container Apps migrator job ----+
```

The two flows share one workflow. Deployment jobs are gated on the event not being a pull request and the ref being `refs/heads/main`, so review happens in GitHub while mutation happens only after integration. A `concurrency` group serialises runs against the shared `dev` stack.

Every `pulumi up` is preceded by a `pulumi preview` whose digest is retained as evidence and inspected for destructive operations. A plan that would delete or replace Azure SQL, ACR, Key Vault, the managed identity, Log Analytics, Application Insights, the Container Apps environment, the container app, or the resource group fails the job before the update runs. The workflow never calls `pulumi destroy`, reuses the existing backend and `dev` stack, and keeps the existing passphrase secrets provider, so repeated runs converge rather than duplicate.

Images carry immutable `<run-id>-<short-sha>` tags and the ACR tags are locked after the build; `latest` is never deployed. Azure access uses OIDC workload identity federation with `id-token: write` and no other elevated permission, so no client secret, PAT, or storage key exists in the delivery path.

## Delivery assets

These are not .NET projects, but they are part of the design and are enforced like it.

| Asset | Responsibility | Allowed to | Prohibited from |
| --- | --- | --- | --- |
| `.github/workflows/ci-cd.yml` | The single CI/CD definition: PR validation and `main` deployment | Reference GitHub secrets by name, call the deployment scripts | Containing secret values; deploying from a pull request; a competing Azure Pipelines definition |
| `scripts/bootstrap-pulumi.ps1` | Discover and select the existing backend and `dev` stack | Reconcile existing config, read the passphrase from the environment | Creating or migrating the backend; changing the secrets provider |
| `scripts/invoke-pulumi-preview.ps1` | Run `pulumi preview`, store the digest, block destructive plans and public Catalog ingress | Fail a stage | Running `pulumi up` or `pulumi destroy` |
| `scripts/run-migrations-dev.ps1` | Start the Container Apps migrator job and await its result | Emit redacted diagnostics | Letting smoke tests run after a failed migration |
| `scripts/azure-containerapp-job.ps1` | Run a one-off command inside the Container Apps Environment as a manual job | Create, start, poll and delete a temporary job | Leaving the job behind; emitting unredacted logs |
| `scripts/internal-smoke-dev.ps1` | Read-only verification of the deployed API from inside the environment | `GET` only, through a Container Apps Job | `POST`, writing permanent data, Playwright, reaching the API from a public runner |
| `scripts/tests/preview-guard-check.ps1` | Offline self-check of the destruction and private-ingress guards | Run in `validate` with no Azure access | Requiring a backend, a login, or the network |
| `scripts/tests/internal-smoke-check.ps1` | Offline self-check of the internal smoke transport | Run in `validate` against a scripted Azure CLI fake | Requiring a subscription, a login, or the network |

The deployment scripts are the shared implementation for both local operation and the workflow, so CI/CD adds orchestration and evidence rather than a second, divergent deployment path. That is also what made replacing the orchestrator cheap: moving from Azure Pipelines to GitHub Actions changed the workflow file and left every script untouched.

Infrastructure compiles independently. Architecture tests enforce that production assemblies cannot reference Pulumi or the Pulumi project `Catalog.Infrastructure`, that the API cannot execute migrations, and that the migrator cannot expose an ASP.NET Core application.

## Volatility

| Volatility | Stable boundary | Implemented decision |
| --- | --- | --- |
| HTTP contracts | `Catalog.Clients.Api` contracts plus `openapi/catalog-api.yaml` | Stable Product, Category, collection, status, health, correlation, and error shapes. |
| Product rules | `ProductRules` (domain) | SKU/text normalization, price validation, optional Category ID, active default, controlled ID/time. |
| Category rules | `CategoryRules` (domain) | Required name, collapsed whitespace, lowercase comparison name, optional description, controlled ID/time. |
| Product-Category association | `CreateProduct` plus repository ports | The use case verifies existence; the SQL foreign key remains authoritative during races. |
| Collection queries | Application ports implemented by `Catalog.Infrastructure.Sql` | Deterministic ordering and one query per collection; no N+1 access. |
| Information containers | `src/Infrastructure/<Name>` | SQL is current; another store (files, an API, an object store) is a new infrastructure adapter of the same ports. |
| SQL engine | SQL adapter registration (`AddCatalogSql`) | SQL Server is Azure SQL compatible and verified by isolated container tests when Docker is available. |
| Uniqueness and integrity | Use-case pre-checks plus SQL constraints | Pre-checks provide clear errors; unique indexes and the foreign key protect concurrency. |
| Time and IDs | `TimeProvider` and ID generators | Domain rules never call the system clock or GUID generator. |
| Errors | Use-case exceptions translated in API middleware | Functional failures and unexpected failures remain distinct, correlated, and safe. |
| Container runtime | Dockerfiles and Container Apps templates | .NET 10 multi-stage, Release-only, port 8080, non-root runtime images. |
| Database lifecycle | `Catalog.Clients.DatabaseMigrator` and Container Apps Job | Migrations are explicit and isolated from the API runtime. |
| Cloud resources | Pulumi project `infra/Catalog.Infrastructure` | Pulumi Azure Native components own dev infrastructure without becoming a production dependency. |
| Secret delivery | Pulumi encrypted config, Key Vault, managed identity | SQL credentials stay encrypted; workloads consume a Key Vault reference. |
| SQL network | VNet, Private Endpoint, private DNS | Public SQL access is disabled; the Container Apps environment owns the application path. |
| Telemetry | Optional Azure Monitor OpenTelemetry registration | Local execution works without Application Insights; Azure uses managed-identity ingestion. |
| Deployment orchestration | PowerShell scripts in `scripts/`, orchestrated by `.github/workflows/ci-cd.yml` | The same scripts serve local operation and CI/CD, so automation adds orchestration and evidence instead of a divergent second path. Replacing Azure Pipelines with GitHub Actions changed only the workflow file. |
| CI/CD provider | `.github/workflows/ci-cd.yml` as the only definition | GitHub Actions runs CI/CD; no Azure Pipelines definition and no competing deployment pipeline exist. |
| Promotion boundary | `github.event_name` and `github.ref` gate | Pull requests validate and preview only; deployment jobs run exclusively for pushes to `main`. |
| Infrastructure destruction risk | `scripts/invoke-pulumi-preview.ps1` | Every update is preceded by a preview; a plan deleting or replacing protected Azure resources fails the job, and `pulumi destroy` is never automated. |
| Image identity | Immutable `<run-id>-<short-sha>` ACR tags, locked after build | A deployed revision is always traceable to one commit and one run; `latest` is never deployed and rollback repoints to a known-good tag. |
| Cloud credentials | OIDC workload identity federation | Deployment holds no client secret, PAT, or storage key; the two secrets stay in GitHub repository secrets and never enter the repository. |

The volatile infrastructure never changes the domain, the use cases or the public contracts.
