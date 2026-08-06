# Catalog Volatility Analysis

| Volatility | Stable boundary | Implemented decision |
| --- | --- | --- |
| HTTP contracts | `Catalog.Contracts` plus `openapi/catalog-api.yaml` | One Product representation, one Category representation, collection envelopes, status request, correlation, and stable errors. |
| Product creation rules | `ProductEngine` | SKU/text normalization, decimal price validation, optional Category ID, active default, controlled ID/time. |
| Product state rules | `ProductEngine` | One status transition operation; baseline-compatible timestamp update even when the requested state repeats. |
| Category rules | `CategoryEngine` | Required name, collapsed whitespace, lowercase comparison name, optional description, controlled ID/time. |
| Product–Category association | `ProductManager` plus domain ports | Manager verifies existence; Product Engine remains deterministic; SQL foreign key protects races. |
| Collection queries | Domain ports implemented by `Catalog.Accessors` | Products use `createdAt DESC, id DESC`; Categories use normalized name and ID ascending; no pagination or N+1 queries. |
| Persistence | `Catalog.Accessors` | EF entities and mappings remain outside business components. |
| SQL engine | Accessor provider registration | SQL Server chosen and Azure SQL compatible; real constraints are verified with an ephemeral SQL Server. |
| Uniqueness | Manager coordination plus SQL indexes | Pre-checks provide clear outcomes; unique SKU and normalized Category name indexes remain authoritative. |
| Referential integrity | SQL foreign key translated by Accessors | Nullable Product Category reference uses `ON DELETE SET NULL`; unknown references become `CATEGORY_NOT_FOUND`. |
| Time and IDs | `TimeProvider`, Product/Category ID generators | Engines receive values and never call the system clock or GUID generator. |
| Errors | Manager exceptions translated in API middleware | Validation, conflicts, Product not found, Category not found, and unexpected errors remain distinct and safe. |
| Deployment | Future pipeline/infrastructure boundary | Validation CI only; Azure deployment remains pending. |

`Catalog.Accessors` is the general information-container access layer. SQL is the current implementation; future file, API, or object-store access belongs behind domain-owned ports in the same layer.
