# Catalog System Design

## Components

- **Client — `Catalog.Api`:** HTTP binding/translation, correlation, safe logs, middleware, composition, health, and OpenAPI.
- **Managers — `Catalog.Managers`:** Product and Category use-case coordination, time/ID acquisition, and mapping.
- **Engines — `Catalog.Engines`:** deterministic Product/Category models, rules, stable ID validation, and provider-neutral Accessor ports.
- **Accessors — `Catalog.Accessors`:** EF Core mapping and SQL Server access; future information-container implementations share this technical boundary.
- **Contracts — `Catalog.Contracts`:** provider-neutral public requests, responses, collection envelopes, and errors.

## Implemented flows

```text
List Products
API -> Product Manager -> Product Accessor -> SQL Server
```

```text
Change Product Status
API -> Product Manager -> Product Engine -> Product Accessor -> SQL Server
```

```text
Create Category
API -> Category Manager -> Category Engine -> Category Accessor -> SQL Server
```

```text
Create Product With Category
API -> Product Manager -> Product Engine -> Category Accessor -> Product Accessor -> SQL Server
```

Product validation runs before the Accessor checks. Within valid creation, SKU existence is checked before Category existence to preserve baseline behavior. Product Engine never queries Category persistence.

## Dependency direction

```text
Catalog.Api -> Catalog.Contracts / Catalog.Managers / Catalog.Accessors (composition only)
Catalog.Managers -> Catalog.Engines
Catalog.Accessors -> Catalog.Engines

Catalog.Engines -X-> ASP.NET Core / EF Core / SQL / Azure / Accessors
Catalog.Managers -X-> ASP.NET Core / EF Core / SQL / Azure / Accessors implementations
```

Executable architecture tests inspect assembly references and endpoint IL. Product and Category endpoint delegates cannot call Engines, Accessors, EF Core, or SQL directly.

## Persistence and concurrency

`InitialProduct` creates Products. `AddCategoriesAndProductCategory` incrementally creates Categories and adds nullable `CategoryId` without rebuilding Product data. SQL Server enforces unique SKU, unique normalized Category name, and the Product–Category foreign key. Accessors translate known provider failures into safe domain-port exceptions.

Collection reads use one `AsNoTracking` query each and project only their persisted models. No Category object is loaded for each Product because the public Product contract requires only `categoryId`.

## Error and observability decision

Engines use controlled exceptions for invalid commands. Managers translate those into use-case errors, and global middleware maps them to stable HTTP envelopes. Unexpected failures produce a safe 500. Structured logs contain correlation, stable IDs, counts, state, and normalized conflict keys; they exclude request bodies, SQL, secrets, and provider details.
