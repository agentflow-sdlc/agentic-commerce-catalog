# Catalog Volatility Analysis

| Volatility | Stable boundary | Implemented decision |
| --- | --- | --- |
| HTTP contracts | `Catalog.Contracts` plus `openapi/catalog-api.yaml` | Stable Product, Category, collection, status, health, correlation, and error shapes. |
| Product rules | `ProductEngine` | SKU/text normalization, price validation, optional Category ID, active default, controlled ID/time. |
| Category rules | `CategoryEngine` | Required name, collapsed whitespace, lowercase comparison name, optional description, controlled ID/time. |
| Product-Category association | `ProductManager` plus domain ports | Manager verifies existence; the SQL foreign key remains authoritative during races. |
| Collection queries | Domain ports implemented by `Catalog.Accessors` | Deterministic ordering and one query per collection; no N+1 access. |
| Information containers | `Catalog.Accessors` | SQL is current; future file, API, object-store, or other container access stays in this layer. |
| SQL engine | Accessor provider registration | SQL Server is Azure SQL compatible and verified by isolated container tests when Docker is available. |
| Uniqueness and integrity | Manager coordination plus SQL constraints | Pre-checks provide clear errors; unique indexes and the foreign key protect concurrency. |
| Time and IDs | `TimeProvider` and ID generators | Engines never call the system clock or GUID generator. |
| Errors | Manager exceptions translated in API middleware | Functional failures and unexpected failures remain distinct, correlated, and safe. |
| Container runtime | Dockerfiles and Container Apps templates | .NET 10 multi-stage, Release-only, port 8080, non-root runtime images. |
| Database lifecycle | `Catalog.DatabaseMigrator` and Container Apps Job | Migrations are explicit and isolated from the API runtime. |
| Cloud resources | `Catalog.Infrastructure` | Pulumi Azure Native components own dev infrastructure without becoming a production dependency. |
| Secret delivery | Pulumi encrypted config, Key Vault, managed identity | SQL credentials stay encrypted; workloads consume a Key Vault reference. |
| SQL network | VNet, Private Endpoint, private DNS | Public SQL access is disabled; the Container Apps environment owns the application path. |
| Telemetry | Optional Azure Monitor OpenTelemetry registration | Local execution works without Application Insights; Azure uses managed-identity ingestion. |
| Deployment orchestration | PowerShell scripts in `scripts/`, orchestrated by `.github/workflows/ci-cd.yml` | The same scripts serve local operation and CI/CD, so automation adds orchestration and evidence instead of a divergent second path. Replacing Azure Pipelines with GitHub Actions changed only the workflow file. |
| CI/CD provider | `.github/workflows/ci-cd.yml` as the only definition | GitHub Actions runs CI/CD; no Azure Pipelines definition and no competing deployment pipeline exist. |
| Promotion boundary | `github.event_name` and `github.ref` gate | Pull requests validate and preview only; deployment jobs run exclusively for pushes to `main`. |
| Infrastructure destruction risk | `scripts/invoke-pulumi-preview.ps1` | Every update is preceded by a preview; a plan deleting or replacing protected Azure resources fails the job, and `pulumi destroy` is never automated. |
| Image identity | Immutable `<run-id>-<short-sha>` ACR tags, locked after build | A deployed revision is always traceable to one commit and one run; `latest` is never deployed and rollback repoints to a known-good tag. |
| Cloud credentials | OIDC workload identity federation | Deployment holds no client secret, PAT, or storage key; the two secrets stay in GitHub repository secrets and never enter the repository. |

The volatile infrastructure implementation never changes the provider-neutral domain or public contracts. Production assemblies cannot reference Pulumi or the infrastructure project.
