# Catalog Volatility Analysis

## Business volatility

| Volatility | Stable boundary | Current status |
| --- | --- | --- |
| Product rules | `Catalog.Engines` | Historical rules documented; implementation deferred. |
| Category rules | `Catalog.Engines` | Historical rules documented; implementation deferred. |
| Use-case sequencing | `Catalog.Managers` | Product and Category flows deferred. |
| Public API contract | `Catalog.Contracts` and `openapi/catalog-api.yaml` | Health implemented; other operations preserved and pending. |

Business rules must remain independent of HTTP, databases, files, cloud SDKs, and deployment technology.

## Technology volatility

| Volatility | Stable boundary | Current status |
| --- | --- | --- |
| Persistence and database engine | `Catalog.Accessors` | No provider selected or implemented. |
| File or object storage | `Catalog.Accessors` | No file accessor implemented. |
| External information sources | `Catalog.Accessors` | Future APIs or containers attach here. |
| HTTP exposure | `Catalog.Api` | ASP.NET Core health vertical implemented. |
| Deployment | Pipeline and future infrastructure | Validation only; no Azure resources. |
| Observability | API middleware | Local structured logging and correlation only. |
| Future integrations | Managers plus Accessors | No integration implemented. |

`Catalog.Accessors` means access to information containers generally. It is not restricted to SQL and must not absorb business rules.
