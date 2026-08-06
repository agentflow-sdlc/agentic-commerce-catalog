# Catalog Project Design

| Project | Responsibility | Allowed dependencies | Prohibited dependencies |
| --- | --- | --- | --- |
| `Catalog.Contracts` | Product, Category, health, collection, status, and error contracts | .NET runtime | Other production projects, ASP.NET Core, EF Core, Azure |
| `Catalog.Api` | Endpoints, HTTP mapping, middleware, correlation, logs, composition, telemetry | Contracts, Managers, Accessors registration, Azure telemetry SDK | Domain rules, direct Accessor calls, EF migration APIs, Pulumi |
| `Catalog.Managers` | Product and Category coordination, mapping, time and ID usage | Engines and domain ports | Accessor implementations, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Engines` | Models, deterministic rules, Accessor ports | .NET runtime | API, Managers, Accessors, ASP.NET Core, EF Core, SQL, Azure |
| `Catalog.Accessors` | SQL access, mapping, constraints, migrations | Engines ports, EF Core, SQL provider | HTTP, use-case coordination, business decisions, Pulumi |
| `Catalog.DatabaseMigrator` | Apply EF Core migrations in a one-shot process | Accessors, generic host | ASP.NET Core, endpoints, infrastructure, business coordination |
| `Catalog.Infrastructure` | Provision Azure dev resources with Pulumi C# | Pulumi Azure Native | References from any production project |
| `Catalog.Api.Tests` | In-memory HTTP and OpenAPI evidence | API, Contracts, boundary fakes | Production use |
| `Catalog.Product.Tests` | Product and Category Engine/Manager tests | Engines, Managers | Production use |
| `Catalog.Product.IntegrationTests` | Complete migration chain and real SQL evidence | Production projects, Testcontainers | Production use |
| `Catalog.Architecture.Tests` | Assembly and endpoint-IL dependency enforcement | Production assemblies, test framework | Production use |

The name `Catalog.Accessors` is deliberately broader than repositories. SQL, files, APIs, object stores, and other information containers belong here when a domain-owned port requires them.

Infrastructure compiles independently. Architecture tests enforce that production assemblies cannot reference Pulumi or `Catalog.Infrastructure`, that the API cannot execute migrations, and that the migrator cannot expose an ASP.NET Core application.
