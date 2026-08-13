# Catalog Azure Infrastructure

This Pulumi C# project provisions the development-only Azure runtime for Catalog. It uses `Pulumi.AzureNative`, component resources, deterministic names, common tags, and the active Azure CLI subscription.

## Existing backend and stack

The backend is bootstrap infrastructure external to the Catalog stack:

- Resource Group: `rg-agentic-pulumi-state`.
- Exactly one existing Storage Account containing the private `pulumi-state` container.
- Backend URL: `azblob://pulumi-state?storage_account=<discovered-account>`.
- Authentication: Azure CLI and Azure RBAC; no account key or SAS token.
- Stack: `dev`.
- Secrets provider: passphrase supplied only through `PULUMI_CONFIG_PASSPHRASE`.

`scripts/bootstrap-pulumi.ps1` discovers and validates these resources. It never creates, replaces, or migrates the backend. The stack SQL password is configured from `CATALOG_SQL_ADMIN_PASSWORD` through standard input and stored only as an encrypted `secure:` value.

## Components and resources

`CatalogFoundation` creates:

- Catalog Resource Group.
- ACR Basic with admin and anonymous pull disabled.
- Log Analytics with 30-day retention.
- Workspace-based Application Insights with local ingestion authentication disabled.
- User-assigned managed identity.
- RBAC-enabled application Key Vault with soft delete and purge protection.
- Azure SQL logical server and Basic database.
- VNet, delegated Container Apps subnet, Private Endpoint subnet, SQL Private Endpoint, and private DNS.
- Azure Container Apps environment connected to the VNet and Log Analytics.
- Encrypted SQL connection string stored as the `catalog-db` Key Vault secret.

`catalog:sqlLocation` places Azure SQL in a subscription-enabled region while the Private Endpoint remains in the application VNet. The SQL server name includes a normalized region segment, preventing global-name collisions during an authorized fallback. All non-SQL resources remain in the backend-discovered primary region.

`CatalogWorkload` is created only when `catalog:deployWorkload=true` and adds:

- Catalog API Container App with HTTPS ingress, port 8080, health probes, and scale-to-zero.
- Manual Container Apps Job for EF Core migrations.
- `AcrPull`, `Key Vault Secrets User`, and `Monitoring Metrics Publisher` role assignments scoped to the required resources.

## Two-step deployment

`pwsh ./scripts/deploy-dev.ps1` performs:

1. Local validation and Pulumi C# compilation.
2. Foundation preview and update with workloads disabled on a new stack.
3. ACR Tasks builds for the API and migrator; Docker is not required locally. **This subscription currently refuses ACR Tasks (`TasksOperationsNotAllowed`)**, so this step fails until an Azure support request lifts it. CI does not depend on it: `.github/workflows/ci-cd.yml` builds with the runner's Docker daemon and pushes with an Entra token.
4. Immutable tags based on commit SHA and UTC build timestamp.
5. Workload preview and update with exact image references.
6. Migration job start and completion check.
7. read-only smoke tests for `/health`, `/products`, and `/categories`.

Subsequent runs keep workloads enabled, build new immutable image tags, and apply the complete stack without temporarily deleting workloads.

## Secrets

Never commit or print the Pulumi passphrase, SQL password, connection string, Azure tokens, registry credentials, or Key Vault values. `Pulumi.dev.yaml` is versionable only when the SQL value is under `secure:` and passphrase metadata is present. Do not run `pulumi config --show-secrets`.

SQL authentication is a temporary MVP exception. The application receives only a Key Vault-backed Container Apps secret reference through its managed identity. Moving Azure SQL to Microsoft Entra-only authentication is a future hardening task.

## Migrations and smoke tests

The API never calls `Database.MigrateAsync()`. The non-HTTP migrator job is the only Azure migration path. A failed job stops deployment before smoke tests and collects only redacted diagnostic lines.

Smoke tests perform GET requests only, validate HTTP 200, JSON, `X-Correlation-ID`, collection shapes, and absence of obvious internal data. Evidence is stored under ignored `artifacts/deployment/dev/<commit-sha>/`.

## Rollback

Application rollback uses a previously published immutable image reference:

1. Set `catalog:apiImage` and `catalog:migratorImage` to the known-good tags.
2. Run `pulumi preview` and review the workload-only change.
3. Run `pulumi up --yes`.
4. Run the migration job only when the target release requires a forward-compatible migration.
5. Repeat the smoke tests.

Database migrations are forward-only in this MVP. A destructive database rollback requires explicit authorization and a separately reviewed recovery plan.

## Destroy safety

`pulumi destroy` removes Catalog stack resources and can delete durable data. It must never be automated or run without explicit authorization and a reviewed target stack. The external state Resource Group, Storage Account, and container are not owned by this stack and are not deleted by Catalog destroy.

## Cost profile

Development defaults favor low cost:

- ACR Basic.
- Azure SQL Basic with 2 GB maximum size.
- Container Apps at 0.25 CPU and 0.5 GiB, API minimum replicas 0 and maximum 2.
- Log Analytics and Application Insights retention limited to 30 days.

Resources that may incur cost without user traffic include Azure SQL, ACR storage/builds, Private Endpoint, Log Analytics/Application Insights ingestion and retention, Key Vault operations, and Container Apps environment/network consumption. Exact prices vary by subscription and region.

## GitHub Actions configuration

`.github/workflows/ci-cd.yml` runs this same flow automatically from GitHub:

- Azure access through `azure/login` with **OIDC workload identity federation** — no client secret. The workflow declares `id-token: write` and `contents: read`.
- Repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`, `PULUMI_BACKEND_URL`, `PULUMI_STACK`, `CATALOG_SQL_ADMIN_LOGIN`.
- Repository secrets `PULUMI_CONFIG_PASSPHRASE` and `CATALOG_SQL_ADMIN_PASSWORD`.

The Entra application backing the federation carries four federated credentials, two subject formats for each of `:ref:refs/heads/main` and `:pull_request`:

```text
repo:<owner>/<repo>:...                        classic subject
repo:<owner>@<org-id>/<repo>@<repo-id>:...     GitHub immutable-identifier subject
```

Both are registered on purpose. This organization currently issues immutable-identifier subjects, and a credential registered only in the classic format fails with `AADSTS700213: No matching federated identity record found`. Keeping both means renaming the organization or the repository does not break deployments, and neither does GitHub changing the default format.

Beyond `Contributor` on the subscription the identity needs:

| Role | Scope | Why |
| --- | --- | --- |
| `Storage Blob Data Contributor` | the Pulumi state Storage Account | Read and write the `azblob` backend |
| `Role Based Access Control Administrator` | the Catalog resource group | `CatalogWorkload` creates the `AcrPull`, `Key Vault Secrets User`, and `Monitoring Metrics Publisher` assignments |
| `Key Vault Secrets Officer` | the Catalog Key Vault | Manage the `catalog-db` secret |

Pull requests execute `validate` and `infrastructure-preview` only. Deployment jobs are gated on the event not being a pull request and the ref being `refs/heads/main`.

`scripts/invoke-pulumi-preview.ps1` runs before every update and fails the job when the plan would delete or replace protected infrastructure, so the destroy safety rule above is enforced mechanically and not only by convention.

## Out of scope

- Production resources or SKUs.
- Microsoft.Playwright external verification after deployment stabilizes.
- Agents, webhooks, inventory, orders, or other business capabilities.
