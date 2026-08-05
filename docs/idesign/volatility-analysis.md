# Catalog Volatility Analysis

## Business volatility

| Volatility | Stable boundary | Current status |
| --- | --- | --- |
| Product rules | `Catalog.Engines` | Create rules implemented; later Product behavior remains pending. |
| Category rules | `Catalog.Engines` | Deferred. |
| Use-case sequencing | `Catalog.Managers` | Create and get-by-ID implemented. |
| Required information access | Domain ports in `Catalog.Engines` | `IProductAccessor` implemented by SQL Accessors. |
| Public API contract | `Catalog.Contracts` and `openapi/catalog-api.yaml` | Health, create Product, and get Product implemented. |

Business rules and required-access contracts remain independent of HTTP, databases, files, cloud SDKs, and deployment technology.

## Technology volatility

| Volatility | Stable boundary | Current status |
| --- | --- | --- |
| SQL persistence | `Catalog.Accessors` | EF Core SQL Server implementation; Azure SQL compatible. |
| File or object storage | `Catalog.Accessors` | No file or object-store accessor implemented. |
| External information sources | `Catalog.Accessors` | No external API accessor implemented. |
| HTTP exposure | `Catalog.Api` | ASP.NET Core endpoints and translation implemented. |
| Deployment | Pipeline and future infrastructure | Validation only; no Azure resources. |
| Observability | API middleware | Local structured logging and correlation only. |
| Future integrations | Managers plus domain ports and Accessors | No external integration implemented. |

`Catalog.Accessors` means access to information containers generally. It is not restricted to SQL and must not absorb business rules or use-case sequencing.
