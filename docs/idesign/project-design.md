# Catalog Project Design

| Project | Responsibility | Allowed dependencies | Prohibited dependencies |
| --- | --- | --- | --- |
| `Catalog.Contracts` | Product, Category, health, collection, status, and error contracts | .NET runtime | Other production projects, ASP.NET Core, EF Core, Azure |
| `Catalog.Api` | Endpoints, HTTP mapping, middleware, correlation, logs, composition, telemetry | Contracts, Managers, Accessors registration, Azure telemetry SDK | Domain rules, direct Accessor calls, EF migration APIs, Pulumi |
| `Catalog.Managers` | Product and Category coordination, mapping, time and ID usage | Engines and domain ports | Accessor implementations, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Engines` | Models, deterministic rules, Accessor ports | .NET runtime | API, Managers, Accessors, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Accessors` | SQL access, mapping, constraints, migrations | Engines ports, EF Core, SQL provider | HTTP, use-case coordination, business decisions, Pulumi |
| `Catalog.DatabaseMigrator` | Apply EF Core migrations in a one-shot process | Accessors, generic host | ASP.NET Core, endpoints, infrastructure, business coordination |
| `Catalog.Infrastructure` | Provision Azure dev resources with Pulumi C# | Pulumi Azure Native | References from any production project |
| `Catalog.Api.Tests` | In-memory HTTP and OpenAPI evidence | API, Contracts, boundary fakes | Production use |
| `Catalog.Product.Tests` | Product and Category Engine/Manager tests | Engines, Managers | Production use |
| `Catalog.Product.IntegrationTests` | Complete migration chain and real SQL evidence | Production projects, Testcontainers | Production use |
| `Catalog.Architecture.Tests` | Assembly and endpoint-IL dependency enforcement | Production assemblies, test framework | Production use |

## Delivery assets

These are not .NET projects, but they are part of the design and are enforced like it.

| Asset | Responsibility | Allowed to | Prohibited from |
| --- | --- | --- | --- |
| `azure-pipelines.yml` | The single CI/CD definition: PR validation and `main` deployment | Reference secret variables by name, call the deployment scripts | Containing secret values; deploying from a pull request; a competing GitHub Actions workflow |
| `.azuredevops/templates/pulumi-setup.yml` | Install the pinned Pulumi CLI and the SDK from `global.json` | Prepare an agent | Logging in, selecting a stack, or reading secrets |
| `scripts/bootstrap-pulumi.ps1` | Discover and select the existing backend and `dev` stack | Reconcile existing config, read the passphrase from the environment | Creating or migrating the backend; changing the secrets provider |
| `scripts/invoke-pulumi-preview.ps1` | Run `pulumi preview`, store the digest, block destructive plans | Fail a stage | Running `pulumi up` or `pulumi destroy` |
| `scripts/run-migrations-dev.ps1` | Start the Container Apps migrator job and await its result | Emit redacted diagnostics | Letting smoke tests run after a failed migration |
| `scripts/smoke-test-dev.ps1` | Read-only verification of the deployed API | `GET` only | `POST`, writing permanent data, Playwright |
| `scripts/tests/preview-guard-check.ps1` | Offline self-check of the destruction guard | Run in Validate with no Azure access | Requiring a backend, a login, or the network |

The deployment scripts are the shared implementation for both local operation and the pipeline, so the pipeline adds orchestration and evidence rather than a second, divergent deployment path.

The name `Catalog.Accessors` is deliberately broader than repositories. SQL, files, APIs, object stores, and other information containers belong here when a domain-owned port requires them.

Infrastructure compiles independently. Architecture tests enforce that production assemblies cannot reference Pulumi or `Catalog.Infrastructure`, that the API cannot execute migrations, and that the migrator cannot expose an ASP.NET Core application.
