# Catalog Use Cases

| Use case | Current state | Future responsibility |
| --- | --- | --- |
| Check service health | Implemented | `Catalog.Api` returns runtime metadata without external checks. |
| Create product | Pending | Manager coordinates Product rules and Accessors. |
| Get product | Pending | Manager retrieves through the required information accessor. |
| List products | Pending | Manager coordinates retrieval and public mapping. |
| Change product status | Pending | Engine decides the state transition; Manager persists it. |
| Create category | Pending | Engine normalizes and validates; Manager persists it. |
| List categories | Pending | Manager retrieves ordered categories. |
| Associate category with product | Pending | Product creation validates category existence before persistence. |

Only `GET /health` is executable in this phase. Product and Category behavior is documented from the archived baseline and will not be represented by placeholder services or accessors.
