# Catalog Project Design

| Project | Responsibility | Allowed dependencies | Prohibited dependencies |
| --- | --- | --- | --- |
| `Catalog.Contracts` | Product, Category, collection, status, health, and error contracts | .NET runtime | Other production projects, ASP.NET Core, EF Core, Azure |
| `Catalog.Api` | Product/Category endpoints, HTTP mapping, middleware, correlation, logs, composition | Contracts, Managers, Accessors registration | Domain rules, direct Accessor calls, EF Core, SQL clients |
| `Catalog.Managers` | Product/Category coordination, mapping, time and ID usage | Engines and domain ports | Accessor implementations, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Engines` | Product/Category models, deterministic rules, Accessor ports | .NET runtime | API, Managers, Accessors, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Accessors` | Product/Category SQL access, mapping, constraints, migrations | Engines ports, EF Core, SQL provider | HTTP, use-case coordination, business decisions |
| `Catalog.Api.Tests` | In-memory HTTP and OpenAPI contract evidence | API, Contracts, boundary fakes | Production use |
| `Catalog.Product.Tests` | Product and Category Engine/Manager tests | Engines, Managers | Production use |
| `Catalog.Product.IntegrationTests` | Complete migration chain and real SQL constraint/end-to-end evidence | Production projects, Testcontainers | Production use |
| `Catalog.Architecture.Tests` | Assembly and endpoint-IL dependency enforcement | Production assemblies, test framework | Production use |

The name `Catalog.Accessors` is deliberately broader than repositories. SQL, files, APIs, object stores, and other information containers belong here when a domain-owned port requires them.
