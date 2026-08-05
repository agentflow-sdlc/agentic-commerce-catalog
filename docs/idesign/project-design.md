# Catalog Project Design

| Project | Responsibility | Allowed dependencies | Prohibited dependencies |
| --- | --- | --- | --- |
| `Catalog.Contracts` | Stable shared HTTP contracts | .NET runtime | Other Catalog production projects, ASP.NET Core, Azure, persistence |
| `Catalog.Api` | HTTP boundary and composition root | Contracts, Managers, Accessors only for registration | EF Core, SQL clients, direct accessor invocation, Product rules |
| `Catalog.Managers` | Product use-case coordination | Engines and domain ports | Accessors implementation, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Engines` | Product model, deterministic rules, neutral accessor port | .NET runtime | Api, Accessors, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Accessors` | Access to SQL, files, APIs, object stores, and other information containers | Engines ports and required provider libraries | HTTP concerns, use-case coordination, business decisions |
| `Catalog.Api.Tests` | In-memory HTTP, middleware, and OpenAPI validation | Api, Contracts, domain test doubles | Production use |
| `Catalog.Product.Tests` | Engine and Manager unit tests | Engines, Managers | Production use |
| `Catalog.Product.IntegrationTests` | Real SQL Server migration and end-to-end Product validation | Production projects, Testcontainers | Production use |
| `Catalog.Architecture.Tests` | Executable dependency rules | Production assemblies, test framework | Production use |

`Catalog.Accessors` is deliberately broader than a repository layer. SQL is the first implementation, but all technical reads and writes to information containers belong behind domain-owned ports and Accessor implementations.
