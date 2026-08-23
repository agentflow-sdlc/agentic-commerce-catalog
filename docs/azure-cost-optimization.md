# Azure POC Cost Optimization — Free First

Audit of the deployed `dev` environment and the decisions taken to bring its idle cost as close to zero as the platform allows.

Audited against the live subscription (`Azure subscription 1`) and Pulumi stack `dev` on the existing `azblob` backend. Prices are indicative US East list prices for orientation only; Azure does not guarantee them and this document deliberately avoids claiming an exact monthly total.

## Decision order

1. Free tier
2. Free monthly grant
3. Scale-to-zero / consumption
4. Pay-per-use
5. A permanently paid resource **only** when no viable alternative exists

## Audit

| Resource | Current SKU | Current purpose | Idle cost risk | Free alternative | Action |
| --- | --- | --- | --- | --- | --- |
| `agenticcatalog654964` (ACR) | Basic | Hosts the API and migrator images | **~$5/month fixed**, charged whether or not an image is pulled | GitHub Container Registry (`ghcr.io`), free for public packages | **REMOVE** |
| `pe-sql-agentic-catalog-dev-654964` (Private Endpoint) | Standard | Private path from Container Apps to Azure SQL | **~$7.30/month fixed** plus per-GB processing | Public endpoint restricted to Azure services + TLS + Entra-protected credentials | **REMOVE** |
| `pe-sql-…nic.*` (NIC) | — | Private Endpoint network interface | Billed as part of the endpoint | Removed with the endpoint | **REMOVE** |
| `privatelink.database.windows.net` (Private DNS zone) | — | Resolves the SQL private endpoint | ~$0.50/month per zone | Not needed once the endpoint is gone | **REMOVE** |
| `privatelink…/catalog-vnet-link` (DNS VNet link) | — | Links the zone to the VNet | Included with the zone | Removed with the zone | **REMOVE** |
| `catalog` (Azure SQL Database) | Basic, 5 DTU, 2 GB | Catalog persistence | **~$4.90/month fixed**, charged while the database exists | Azure SQL Database free offer: General Purpose Serverless Gen5 2 vCore, 32 GB, auto-pause, free monthly limit | **REVIEW** — see "Azure SQL" below |
| `sql-agentic-catalog-dev-centralus-654964` (SQL Server) | — | Logical server | No charge for the server itself | — | **KEEP_FREE** |
| `ca-catalog-api-654964` (Container App) | Consumption, 0.25 vCPU / 0.5 GiB, min 0 / max 2 | Catalog API | Already scales to zero; consumption free grant covers light use | Already optimal | **KEEP_FREE** |
| `caj-catalog-migrate-654964` (Container Apps Job) | Consumption, manual trigger | EF Core migrations | Runs only on demand | Already optimal | **KEEP_FREE** |
| `cae-agentic-catalog-dev-654964` (Container Apps env) | Consumption | Hosts the app and job | No charge with zero replicas | Already optimal | **KEEP_FREE** |
| `capp-svc-lb` / `capp-svc-lb-ip` (managed `ME_…` RG) | Standard LB + Standard public IP | Ingress for the Container Apps environment | Microsoft-managed; a Standard public IP is normally billable, but these are created and owned by the platform | Not independently replaceable without deleting the environment | **REVIEW** |
| `log-agentic-catalog-dev-654964` (Log Analytics) | PerGB2018, 30-day retention, **no daily cap** | Telemetry store | 5 GB/month is free; beyond that ~$2.30/GB with **nothing stopping overage** | Same SKU with a daily ingestion cap | **CHANGE_TO_FREE** |
| `appi-agentic-catalog-dev-654964` (App Insights) | Workspace-based, 30-day retention, 100 % sampling | Request/error telemetry | Ingests into Log Analytics; 100 % sampling maximises the risk of exceeding the free grant | Sampling plus quieter log levels | **CHANGE_TO_FREE** |
| `kv-agentic-cat-654964` (Key Vault) | Standard, RBAC, purge protection | SQL connection string for the workload | ~$0.03 per 10 000 operations; effectively cents | Standard is already the cheapest tier | **KEEP_LOW_COST** |
| `id-agentic-catalog-dev-654964` (Managed Identity) | — | ACR pull, Key Vault read, metrics publish | Free | — | **KEEP_FREE** |
| `vnet-agentic-catalog-dev-654964` + subnets | — | Container Apps infrastructure subnet | VNets and subnets are free | — | **KEEP_FREE** |
| `stagenticstate654964` (Storage) | Standard **LRS**, Hot, StorageV2 | Pulumi state backend | Cents per month for a few small blobs | Already the cheapest sensible configuration | **KEEP_LOW_COST** |
| `rg-agentic-pulumi-state` | — | Holds the Pulumi state account | Free | — | **KEEP_FREE** |
| `emanuelabr22-0813-resource` (`Microsoft.CognitiveServices/accounts`, kind `AIServices`, S0, westus3) | S0 | **Unknown — not managed by Pulumi**, sitting in `NetworkWatcherRG` | S0 for AI Services is pay-per-call, so idle cost is ~$0, but the resource is unaccounted for | — | **REVIEW** — see below |
| `NetworkWatcher_eastus2` | — | Created automatically by Azure | Free | — | **KEEP_FREE** |

### Resources not present (confirmed absent, no action needed)

NAT Gateway, Application Gateway, VPN Gateway, Bastion, Azure Firewall, standalone reserved public IPs, dedicated Load Balancers, and any Container Apps dedicated workload profile. The environment runs Consumption only.

### Making Catalog internal costs nothing

Catalog ingress moved from external to internal. That is a security change, not a cost change: Container Apps internal ingress uses the same platform-managed load balancer the environment already has, so no NAT Gateway, no Application Gateway, no Front Door, no API Management and no second environment were introduced.

Post-deployment smoke moved inside the environment for the same reason. It runs as a **manual** Container Apps Job that is created immediately before use and deleted afterwards, so it holds no replica and adds no fixed cost — the same lifecycle as the existing migration job. The execution image is pulled anonymously from Microsoft Container Registry, so it also adds no registry cost and no stored credential.

### The unaccounted AI Services account

`emanuelabr22-0813-resource` is an `AIServices` account on SKU `S0` in `westus3`, inside `NetworkWatcherRG`. It is **not** part of the Pulumi stack, does not appear in any stack output, and nothing in this repository references it. Its naming matches the default an Azure AI Foundry portal quick-create produces.

S0 for AI Services is pay-per-call, so it is not generating idle cost, which is why it is classified `REVIEW` rather than `REMOVE`. It is nevertheless outside infrastructure-as-code and outside the scope of this change, and deleting an unmanaged resource is not a decision to take unattended. **Recommendation:** confirm it is unused and delete it manually, or bring it under Pulumi if it is intentional.

## Container registry — ACR to GHCR

ACR Basic costs a fixed monthly amount regardless of use, which is exactly the cost shape this optimization removes. This repository is public, so its container packages can be public too without exposing anything that is not already public.

Public GHCR packages are pulled anonymously, which means **Azure Container Apps needs no registry credential at all**. That avoids a personal access token, avoids storing a registry secret in Key Vault, and avoids the operational burden of rotating one. `GITHUB_TOKEN` is sufficient to push during the workflow and expires when the job ends, so it never becomes a standing credential.

Ordering is enforced by the workflow itself, so ACR is never removed before its replacement is proven:

1. `build-images` builds and pushes both images to `ghcr.io`.
2. The same job makes the packages public and then verifies an **anonymous** pull actually succeeds.
3. Only if that verification passes does `deploy` run, which is what removes ACR.

A failure at any earlier point stops the run with ACR still in place.

## Azure SQL

The database is `Basic` (5 DTU, 2 GB), a fixed monthly charge for as long as it exists.

The Azure SQL Database free offer would remove that charge: General Purpose Serverless Gen5 2 vCore, up to 32 GB, auto-pause when idle, and a free monthly allowance of vCore-seconds and storage. No database in this subscription currently uses the free limit, so the one-per-subscription allowance is available.

**The free offer cannot be applied to an existing database.** `useFreeLimit` is settable only at creation, so adopting it means creating a new database and therefore destroying the current one.

Under the current safety constraints, **that destruction is not performed**. The code is prepared and gated behind explicit configuration, the change is not applied, and the decision is left to the operator. See `Azure SQL Free Offer status` in the pull request and the guardrail description below.

Until then the database stays `Basic`, which is the cheapest non-free option available in-place, and remains the single largest residual fixed cost.

## Private endpoint and networking

The private endpoint exists only to reach Azure SQL privately. For a POC that is roughly $7.30/month to demonstrate a topology the POC is not trying to demonstrate.

It is replaced by:

- Azure SQL public network access **enabled**;
- the Azure `AllowAllWindowsAzureIps` firewall rule (start and end address `0.0.0.0`), which admits **only traffic originating from Azure services**, not the public internet;
- TLS 1.2 minimum, unchanged;
- credentials that continue to live in Key Vault and reach the workload through its managed identity.

**Implication, stated plainly:** the `0.0.0.0` rule admits connections from any Azure tenant's resources, not merely this subscription's. It is not equivalent to a private endpoint and it is not what production should use. It is chosen here because Container Apps Consumption has no stable outbound IP to allowlist, and because the alternative costs money permanently. Production should restore the private endpoint.

The VNet and its subnets stay: they are free, and the Container Apps environment is VNet-integrated, so removing them would require recreating the environment.

The private DNS zone and its VNet link are removed with the endpoint, as they exist solely to resolve it.

## Observability

Application Insights is kept because observability remains required. What changes is the risk of leaving the free grant:

- a **daily ingestion cap** on the Log Analytics workspace, so overage cannot happen silently;
- Application Insights sampling reduced from 100 %;
- retention held at 30 days, which is within the free retention window;
- quieter default log levels for the noisy `Microsoft.*` and `Microsoft.EntityFrameworkCore.*` categories, with the application's own categories left at `Information`.

What must stay visible: requests, errors, correlation IDs, workflow traces, and deployment evidence.

## Key Vault

One vault exists, not two, so there is nothing to consolidate. It is Standard, not Premium/HSM, RBAC-enabled, and reached through the workload's managed identity. Secrets are not duplicated and nothing is moved into source. Its cost is per-operation and amounts to cents.

## Pulumi state storage

`stagenticstate654964` is already Standard **LRS**, Hot, StorageV2 — the cheapest sensible configuration for this purpose. No geo-replication, no premium tier. It is left untouched: the state lives here, and the risk of touching it outweighs a fraction of a cent.

## Cost guardrails

Two Pulumi configuration values encode "free first" so an expensive resource cannot appear by accident:

| Setting | Default | Effect |
| --- | --- | --- |
| `catalog:costProfile` | `poc-free` | Selects the free/minimum option everywhere a choice exists |
| `catalog:allowPaidResources` | `false` | Must be explicitly `true` before any resource outside the free path is provisioned |

Under `poc-free` the stack will not provision ACR, private endpoints, dedicated Container Apps compute, a production SQL SKU, premium Functions plans, premium Cosmos, a paid Azure AI Search tier, or provisioned model capacity. Anything requiring those must set `allowPaidResources=true` deliberately.

## Tags

Every Pulumi-managed resource carries:

```
environment    = dev
purpose        = agentic-sdlc-poc
cost-profile   = free-first
managed-by     = pulumi
service        = catalog
```

These make Cost Management grouping possible later.

## Future capabilities — free-first strategy

None of the following are currently created. They are recorded so the free path is chosen when they are.

### Azure Functions

When the Agentic SDLC needs to receive events or webhooks, use the **Consumption** plan. It scales to zero and includes a monthly free grant of executions and GB-seconds. Do not use Premium or Dedicated plans for the POC without a demonstrated technical need — Premium keeps warm instances and therefore has a permanent idle cost that conflicts with the optimization goal.

### Azure Cosmos DB

Use **Azure Cosmos DB for NoSQL** with the **free tier enabled at creation time**, targeting ≤ 1000 RU/s and ≤ 25 GB.

Two constraints matter and are easy to get wrong:

- The free tier can only be enabled **when the account is created**; it cannot be turned on afterwards.
- A subscription may have **only one** free-tier account. Check whether it is already consumed before creating anything.

Use a **single** account for the POC, not one per component. Planned containers are `messages`, `workflow-state`, `agent-executions`, and `audit`, sharing throughput at the database level where appropriate so the 1000 RU/s allowance covers all of them.

Do **not** choose Serverless if that would forfeit the lifetime free tier — serverless and free tier are mutually exclusive, and the free tier is the better deal for a POC that idles.

### Microsoft Foundry, Foundry IQ, and Azure AI Search

The intended split, when built:

- **Azure AI Search / Foundry IQ** — enterprise, documentary, and RAG knowledge.
- **Graphify** — the real technical graph of the code.
- **Repomix** — context packs of the code.

Rules for provisioning them:

- **Azure AI Search**: try the **Free** SKU first. Do not create Basic or Standard automatically. If the corpus exceeds the free tier's limits, **fail with a clear message** rather than silently provisioning a paid tier.
- **Foundry IQ / agentic retrieval**: use the free allowance or free plan where one exists. Do not enable Standard billing automatically.
- **Models**: pay-per-token only. No Provisioned Throughput, no reserved capacity. Prefer small, inexpensive models when they deliver the required quality.

Every model call path must configure input token limits, output token limits, a maximum number of calls, a maximum number of retries, a timeout, and consumption telemetry. The POC should spend money when a model actually runs, and generate no meaningful idle cost for AI.

## Budget

A monthly budget of USD 10 with alerts at 50 %, 80 % and 100 % is the intended guardrail. Whether it is created by Pulumi or configured manually is recorded in `docs/azure-cost-baseline.md`, along with the reason.

## Result

See `docs/azure-cost-baseline.md` for the post-change inventory and the idle cost classification.
