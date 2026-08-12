# Azure POC Cost Baseline

The state the `dev` environment reaches once this change is deployed from `main`. The audit and the reasoning behind each decision are in [`azure-cost-optimization.md`](azure-cost-optimization.md).

Verified with a real `pulumi preview` against the existing `dev` stack: **2 to create, 12 to update, 6 to delete, 2 to replace, 5 unchanged**, with no unexpected destruction.

## Removed fixed-cost resources

| Resource | Why it cost money | Replacement |
| --- | --- | --- |
| `agenticcatalog654964` — Azure Container Registry (Basic) | Fixed monthly charge whether or not an image is pulled | Public GitHub Container Registry packages, pulled anonymously |
| `pe-sql-agentic-catalog-dev-654964` — Private Endpoint | Fixed hourly charge plus per-GB processing | Azure SQL public endpoint restricted to Azure services |
| `pe-sql-…nic.*` — Private Endpoint NIC | Part of the endpoint | Removed with it |
| `privatelink.database.windows.net` — Private DNS zone | Per-zone monthly charge | Not needed without the endpoint |
| `privatelink…/catalog-vnet-link` — DNS VNet link | Part of the zone | Removed with it |
| `catalog-sql-private-dns-zone-group` | Part of the endpoint | Removed with it |
| `catalog-acr-pull` — role assignment | No cost, but meaningless once ACR is gone | Anonymous pull needs no role |

Deleting the registry is the only protected-type removal in the plan. It is permitted **only** because it is named explicitly in the preview guard, and only after `build-images` has proven that an anonymous GHCR pull works. Every other protected type still fails the deployment.

## Free-tier resources

| Resource | Why it is free |
| --- | --- |
| `vnet-agentic-catalog-dev-654964` + `snet-container-apps` + `snet-private-endpoints` | Virtual networks and subnets carry no charge |
| `id-agentic-catalog-dev-654964` — Managed Identity | No charge |
| `sql-agentic-catalog-dev-centralus-654964` — SQL logical server | The server is free; only the database is billed |
| `cae-agentic-catalog-dev-654964` — Container Apps environment | No charge with zero running replicas |
| `rg-agentic-catalog-dev-654964`, `rg-agentic-pulumi-state` | Resource groups are free |
| `AllowAllWindowsAzureIps` — SQL firewall rule | Free |
| `catalog-poc-monthly` — Cost Management budget | Budgets are free |
| GitHub Container Registry packages | Free for public packages, with no egress charge to Azure |

## Scale-to-zero resources

| Resource | Behaviour |
| --- | --- |
| `ca-catalog-api-654964` — Container App | `minReplicas: 0`, `maxReplicas: 2`, 0.25 vCPU / 0.5 GiB, Consumption profile. Idles at zero replicas and therefore at effectively zero compute cost. Cold start is accepted. |
| `caj-catalog-migrate-654964` — Container Apps Job | Manual trigger. Runs only during a deployment and holds nothing between runs. |

Container Apps Consumption also carries a monthly free grant of vCPU-seconds, GiB-seconds and requests, which light POC use stays inside.

## Resources that may still generate cents

| Resource | What drives the cost | Control in place |
| --- | --- | --- |
| `catalog` — Azure SQL Database (Basic, 5 DTU, 2 GB) | **Fixed monthly charge while it exists.** This is the single largest residual cost | Not removable without the free offer; see below |
| `log-agentic-catalog-dev-654964` — Log Analytics | 5 GB/month free, then per-GB | **Daily cap of 0.1 GB** so overage cannot happen silently; 30-day retention |
| `appi-agentic-catalog-dev-654964` — Application Insights | Ingests into the workspace above | Sampling reduced to 25 %; `Microsoft.*` and EF Core categories at `Warning` |
| `kv-agentic-cat-654964` — Key Vault | Per-operation, roughly cents | Standard tier, one vault, secrets read through managed identity |
| `stagenticstate654964` — Pulumi state storage | Per-GB and per-transaction on a few small blobs | Already Standard LRS / Hot; left untouched to protect the state |
| `capp-svc-lb` / `capp-svc-lb-ip` in the managed `ME_…` group | Platform-managed load balancer and Standard public IP for the Container Apps environment | Not independently controllable; would require deleting the environment. Classified as a residual unknown rather than claimed to be free |
| Container App egress | Outbound data transfer | Negligible at POC traffic |

### Azure SQL free offer — not applied

The database is still `Basic`. Moving it to the free offer (General Purpose Serverless Gen5 2 vCore, 32 GB, auto-pause, free monthly limit) would remove this cost entirely.

**It was not applied, on purpose.** `useFreeLimit` can only be set when a database is created, so switching means Pulumi replaces the database and **destroys its data**. This change does not destroy persistent resources.

The code is written and verified to compile, behind its own opt-in flag:

```
catalog:sqlUseFreeOffer = false   # default
```

Setting it to `true` provisions `GP_S_Gen5` capacity 2, `MinCapacity 0.5`, `AutoPauseDelay 60`, `UseFreeLimit true`, `FreeLimitExhaustionBehavior AutoPause`, 32 GB. Before enabling it:

1. Take a backup or export of the current database — the change is destructive.
2. Confirm the subscription's single free-tier database allowance is still unused (it currently is).
3. Run `pulumi preview` and confirm the replacement is the only destructive operation.

The preview guard will refuse the run unless `azure-native:sql:Database` is explicitly named as an allowed deletion, which is deliberate: this must be a conscious act.

## Future Functions strategy

Not currently created. When the Agentic SDLC needs to receive events or webhooks, use the **Consumption** plan: it scales to zero and includes a monthly free grant of executions and GB-seconds. Premium and Dedicated plans keep instances warm and therefore reintroduce permanent idle cost; they require a demonstrated technical need and `allowPaidResources=true`.

## Future Cosmos Free Tier strategy

Not currently created. When needed, use **Azure Cosmos DB for NoSQL** with the **free tier enabled at creation**, targeting ≤ 1000 RU/s and ≤ 25 GB.

Two things are easy to get permanently wrong:

- The free tier can only be enabled **at account creation**. It cannot be turned on later.
- A subscription may hold **only one** free-tier account. Verify the allowance is unused before creating anything.

Use a **single** account for the whole POC. Planned containers — `messages`, `workflow-state`, `agent-executions`, `audit` — should share database-level throughput so the 1000 RU/s allowance covers all of them. Do not choose Serverless if that forfeits the lifetime free tier; for a workload that idles, the free tier is the better deal.

## Future Foundry free-first strategy

Not currently created. Intended split:

- **Azure AI Search / Foundry IQ** — enterprise, documentary and RAG knowledge.
- **Graphify** — the real technical graph of the code.
- **Repomix** — context packs of the code.

Provisioning rules:

- **Azure AI Search**: the **Free** SKU first. Never create Basic or Standard automatically. If the corpus exceeds the free tier, **fail with a clear message** rather than silently provisioning a paid tier.
- **Foundry IQ / agentic retrieval**: free allowance or free plan where one exists; never enable Standard billing automatically.
- **Models**: pay-per-token only. No Provisioned Throughput, no reserved capacity. Prefer small, inexpensive models where quality allows.
- Every model call path configures input and output token limits, a maximum call count, a maximum retry count, a timeout, and consumption telemetry.

All of this is gated behind `allowPaidResources`, which defaults to `false`.

There is one loose end already in the subscription: `emanuelabr22-0813-resource`, an `AIServices` S0 account in `NetworkWatcherRG` (westus3) that is **not managed by Pulumi**. S0 is pay-per-call, so it is not generating idle cost, but it is unaccounted for. Confirm it is unused and delete it, or bring it under IaC.

## Expected idle cost

**Classification: `NEAR_ZERO`.**

With no traffic, the environment charges for:

- the Azure SQL Basic database — a small fixed monthly amount, the dominant remaining item;
- Key Vault operations — cents;
- Pulumi state storage — cents;
- the platform-managed Container Apps load balancer and public IP — a residual unknown.

Everything else idles at zero: no registry, no private endpoint, no private DNS zone, zero container replicas, and telemetry that is capped rather than open-ended.

The result would become **`FREE`** for practical purposes once the Azure SQL free offer is adopted, which is the single remaining step and is deliberately left to an operator because it destroys data.

No exact monthly figure is stated here. Azure does not guarantee one, prices vary by subscription agreement and region, and inventing a number would be more misleading than useful.

## Expected demo-variable costs

**Classification: `VARIABLE`**, and only while something is actually being demonstrated:

- Container Apps vCPU-seconds and GiB-seconds while replicas are awake, inside the monthly free grant for light use;
- Log Analytics and Application Insights ingestion, bounded by the 0.1 GB daily cap;
- SQL serverless compute, if and when the free offer is adopted, bounded by auto-pause;
- container image pulls from GHCR, which are free;
- future model calls, which are pay-per-token by design and produce no idle cost.

## Guardrails now in place

| Setting | Default | Effect |
| --- | --- | --- |
| `catalog:costProfile` | `poc-free` | Selects the free/minimum option wherever a choice exists |
| `catalog:allowPaidResources` | `false` | Anything off the free path must opt in explicitly; a non-free `costProfile` without it fails the stack |
| `catalog:sqlUseFreeOffer` | `false` | Separate opt-in, because adopting it destroys the database |
| `catalog:budgetAmountUsd` | `10` | Monthly budget on the catalog resource group |
| `catalog:budgetContactEmail` | — | No address, no budget: an unnotified budget protects nothing |

Under `poc-free` the stack will not provision ACR, private endpoints, dedicated Container Apps compute, a production SQL SKU, premium Functions plans, premium Cosmos, a paid Azure AI Search tier, or provisioned model capacity.

The preview guard refuses to delete or replace Azure SQL, the SQL server, ACR, Key Vault, the managed identity, Log Analytics, Application Insights, the Container Apps environment, the container app or the resource group unless that type is explicitly named as an allowed removal.

## Tags

Every Pulumi-managed resource carries `environment=dev`, `purpose=agentic-sdlc-poc`, `cost-profile=free-first`, `managed-by=pulumi`, `project=agentic-sdlc` and `service=catalog`, so Cost Management can group POC spend.

## Budget

A monthly USD 10 budget named `catalog-poc-monthly` is created by Pulumi on the catalog resource group, with actual-cost alerts at 50 %, 80 % and 100 %. Budgets are free. It is scoped to the resource group rather than the subscription so POC spend is not confused with anything else.
