# Catalog Use Cases

| Use case | State | Implemented flow |
| --- | --- | --- |
| Check health | Implemented | API returns service metadata and correlation header. |
| Create Product | Implemented | Manager validates through Engine, checks SKU/Category through ports, and persists through Product Accessor. |
| Get Product | Implemented | Engine validates ID before Manager queries the Product Accessor. |
| List Products | Implemented | Manager requests the deterministically ordered collection from Product Accessor. |
| Change Product status | Implemented | Manager loads Product, Engine creates the baseline-compatible transition, and Accessor updates state/time. |
| Create Category | Implemented | Manager invokes Category Engine, checks normalized-name uniqueness, and persists through Category Accessor. |
| List Categories | Implemented | Manager returns the deterministically ordered Category Accessor result. |
| Associate Category during Product creation | Implemented | Manager verifies an explicit Category ID; Accessor persists the nullable foreign key. |

## Create Product with optional Category

1. API maps `CreateProductRequest` without creating a Category implicitly.
2. Product Engine validates all Product input before any Accessor call.
3. Manager checks normalized SKU uniqueness.
4. When `categoryId` exists, Manager checks it through `ICategoryAccessor`.
5. Product Accessor inserts Product; SQL unique/FK constraints protect concurrent changes.
6. API returns `201`, Product envelope, `Location`, and correlation ID.

## List Products

Product Accessor issues one no-tracking query ordered by creation time and ID descending. Empty data returns `200` with `data: []`.

## Change Product status

Manager validates and loads Product, then Product Engine returns a copy containing only the requested state and controlled `updatedAt`. Matching the historical baseline, repeated status requests still refresh `updatedAt`. Accessor performs one targeted SQL update.

## Create and list Categories

Category Engine creates display and normalized names. Manager uses the normalized name for a duplicate pre-check, while the SQL unique index handles races. Category Accessor lists by normalized name then ID; the normalized name never enters the public contract.
