# Catalog Volatility Analysis

## Business volatility

| Volatility | Stable boundary | Current status |
| --- | --- | --- |
| Product rules | `Catalog.Engines` | Create rules implemented; later Product behavior remains pending. |
| Category rules | `Catalog.Engines` | Deferred. |
| Use-case sequencing | `Catalog.Managers` | Create and get-by-ID implemented. |
| Required information access | Domain ports in `Catalog.Engines` | `IProductAccessor` implemented by SQL Accessors. |
| Public API contract | `Catalog.Contracts` and `openapi/catalog-api.yaml` | Health, create Product, and get Product implemented. |
| Stable IDs | `ProductEngine` format plus `IProductIdGenerator` | Application generates `PRODUCT-<guid>`; Manager validates and canonicalizes before reads. |
| Time | .NET `TimeProvider` at the Manager boundary | Engine receives timestamps and remains deterministic. |
| Functional errors | Manager-controlled exceptions translated by `Catalog.Api` | Validation, conflict, and not-found outcomes are explicit and include safe details. |

Business rules and required-access contracts remain independent of HTTP, databases, files, cloud SDKs, and deployment technology.

## Technology volatility

| Volatility | Stable boundary | Current status |
| --- | --- | --- |
| Persistence mapping | `Catalog.Accessors` | EF Core Product entity, Fluent mapping, and migration implemented. |
| SQL engine | `Catalog.Accessors` provider configuration | SQL Server provider selected and Azure SQL compatible; domain remains provider-neutral. |
| File or object storage | `Catalog.Accessors` | No file or object-store accessor implemented. |
| External information sources | `Catalog.Accessors` | No external API accessor implemented. |
| HTTP exposure | `Catalog.Api` | ASP.NET Core endpoints and translation implemented. |
| Deployment | Pipeline and future infrastructure | Validation CI only; Azure deployment remains pending. |
| Observability | API middleware | Structured request and Product use-case logs with correlation and no request bodies. |
| Future integrations | Managers plus domain ports and Accessors | No external integration implemented. |

`Catalog.Accessors` means access to information containers generally. It is not restricted to SQL and must not absorb business rules or use-case sequencing.
