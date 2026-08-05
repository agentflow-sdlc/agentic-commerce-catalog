# Catalog Project Design

| Project | Responsibility | Allowed dependencies | Prohibited dependencies |
| --- | --- | --- | --- |
| `Catalog.Contracts` | Stable shared HTTP contracts | .NET runtime | Other Catalog production projects, ASP.NET Core, Azure, persistence |
| `Catalog.Api` | HTTP client boundary and composition | Contracts; Managers when a real use case exists | Direct Accessors for use cases, Product/Category rules |
| `Catalog.Managers` | Future use-case coordination | Contracts, Engines, required Accessors | ASP.NET Core and HTTP concerns |
| `Catalog.Engines` | Future deterministic business rules | Contracts when a real rule requires them | Api, Accessors, ASP.NET Core, EF Core, Azure, SQL |
| `Catalog.Accessors` | Future access to SQL, files, APIs, object stores, or other information containers | Provider libraries required by implemented accessors | Business decisions and use-case coordination |
| `Catalog.Api.Tests` | In-memory HTTP, middleware, and OpenAPI validation | Api, Contracts, test libraries | Production use |
| `Catalog.Architecture.Tests` | Executable dependency rules | Production assemblies, test framework | Production use |

Managers, Engines, and Accessors contain assembly markers solely so their intended boundaries can be loaded and enforced today. Those markers are not placeholder services or abstractions. New types belong in these projects only when the Product vertical slice introduces real behavior.
