# Catalog System Design

## Components

- **Client — `Catalog.Api`:** ASP.NET Core entry point, dependency composition, HTTP translation, middleware, runtime OpenAPI, and health. It contains no Product business rules or EF queries.
- **Managers — `Catalog.Managers`:** coordinates Create Product and Get Product by ID. It invokes the Engine, obtains IDs and time from abstractions, and uses the domain accessor port.
- **Engines — `Catalog.Engines`:** owns the deterministic Product model, normalization, validation, defaults, and the provider-neutral `IProductAccessor` port. It has no HTTP, EF Core, SQL, Azure, or provider dependency.
- **Accessors — `Catalog.Accessors`:** owns concrete access to information containers. The current SQL accessor maps between the domain Product and an EF persistence entity; later file, API, object-store, or other container access belongs to the same layer.
- **Contracts — `Catalog.Contracts`:** owns provider-neutral HTTP request and response shapes.

## Product request flow

```text
POST /products or GET /products/{id}
  -> Catalog.Api maps HTTP contracts
  -> ProductManager coordinates the use case
  -> ProductEngine applies deterministic rules
  -> IProductAccessor expresses the domain-required access
  -> SqlProductAccessor maps and calls CatalogDbContext
  -> SQL Server or Azure SQL
  -> response is mapped back through Manager and API
```

## Dependency direction

```text
Catalog.Api -> Catalog.Contracts
Catalog.Api -> Catalog.Managers
Catalog.Api -> Catalog.Accessors (composition only)

Catalog.Managers -> Catalog.Engines
Catalog.Accessors -> Catalog.Engines

Catalog.Engines -X-> Catalog.Api / Catalog.Accessors / ASP.NET Core / EF Core / SQL / Azure
Catalog.Managers -X-> Catalog.Accessors / ASP.NET Core / EF Core / SQL / Azure
```

The port is in the domain boundary, not in the Manager or SQL layer. This preserves dependency inversion: the use case and domain describe required information access, and `Catalog.Accessors` supplies the technical implementation.

## Determinism and race safety

The Engine receives both the Product ID and timestamp. It never calls `Guid.NewGuid()` or `DateTime.UtcNow`. The Manager supplies IDs through `IProductIdGenerator` and time through .NET `TimeProvider`, which tests replace deterministically.

The Manager performs a normalized-SKU existence check for a clear conflict result. The database unique index remains authoritative for concurrent requests, and the SQL accessor translates SQL Server unique-key errors into the same provider-neutral conflict.

## Implemented now

- `GET /health`.
- `POST /products`.
- `GET /products/{id}`.
- Stable correlation IDs and error contracts.
- EF Core SQL Server/Azure SQL mapping and initial migration.
- Unit, in-memory HTTP, architecture, OpenAPI, and ephemeral SQL Server tests.

Category, Product listing, status changes, search, inventory, authentication, deployment, infrastructure, and agents remain outside this slice.
