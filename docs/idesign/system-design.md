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

## Error and observability decision

Managers translate controlled domain failures into stable HTTP errors, and global middleware maps unexpected failures to a safe correlated 500. Structured logs exclude request bodies, SQL values, secrets, and provider details. Azure Monitor registration is conditional, so the API still starts locally when Application Insights is absent.
