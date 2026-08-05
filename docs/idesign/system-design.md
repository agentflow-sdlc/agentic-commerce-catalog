# Catalog System Design

## Components

- **Client — `Catalog.Api`:** ASP.NET Core entry point, composition, HTTP translation, middleware, development OpenAPI, and health. It contains no Product or Category rules.
- **Managers — `Catalog.Managers`:** future use-case coordination across Engines and Accessors. It currently contains only an assembly marker required by architecture tests.
- **Engines — `Catalog.Engines`:** future deterministic Product and Category rules. It currently contains only an assembly marker and no business behavior.
- **Accessors — `Catalog.Accessors`:** future technical access to any information container, including SQL databases, files, object stores, external APIs, and other sources. It currently contains only an assembly marker; there are no repositories, provider clients, or persistence abstractions.
- **Contracts — `Catalog.Contracts`:** stable HTTP response contracts required by implemented behavior.
- **Cross-cutting utilities:** correlation, structured logging, safe exception translation, JSON configuration, and centralized service metadata live in the API because that is where they are currently used.

## Dependency direction

```text
Catalog.Api -> Catalog.Contracts

Future use-case direction:
Catalog.Api -> Catalog.Managers
Catalog.Managers -> Catalog.Contracts + Catalog.Engines + required Accessors

Catalog.Engines -X-> Catalog.Api / Catalog.Accessors / ASP.NET Core / EF Core / Azure
```

The API must not call Accessors directly to execute business use cases. Managers own coordination; Engines own deterministic decisions; Accessors only communicate with information containers.

## Implemented now

- `GET /health`.
- Correlation ID generation and propagation.
- Structured request and unexpected-error logging.
- Safe unexpected-error responses without stack traces.
- Camel-case JSON and development OpenAPI generation.
- Automated HTTP, contract, and architecture tests.

There is no persistence, database, Product/Category implementation, remote observability, deployment, authentication, agent runtime, or external integration.
