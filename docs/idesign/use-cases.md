# Catalog Use Cases

| Use case | Current state | Responsibility |
| --- | --- | --- |
| Check service health | Implemented | `Catalog.Api` returns runtime metadata without an external dependency. |
| Create product | Implemented | Manager coordinates deterministic Product creation and persistence through the domain accessor port. |
| Get product by ID | Implemented | Manager retrieves through the domain accessor port and rejects unknown IDs. |
| List products | Pending | Future Manager-coordinated retrieval. |
| Change product status | Pending | Future Engine decision and Manager persistence. |
| Create category | Pending | Future Engine normalization and validation plus Manager coordination. |
| List categories | Pending | Future Manager-coordinated retrieval. |
| Associate category with product | Pending | Explicitly excluded from the current Product model and migration. |

## Create Product

1. API binds `CreateProductRequest` and calls `ProductManager`.
2. Manager supplies an application ID and the current `TimeProvider` timestamp to `ProductEngine`.
3. Engine normalizes and validates SKU, name, description, and decimal price; it applies active state and timestamps.
4. Manager checks the normalized SKU through `IProductAccessor`.
5. `SqlProductAccessor` maps the Product and commits it through EF Core.
6. API returns `201 Created`, a `Location` header, the Product response, and the correlation ID.

## Get Product by ID

1. API passes the route ID to `ProductManager`.
2. Engine validates and canonicalizes the `PRODUCT-<guid>` ID before any persistence call.
3. Manager retrieves through `IProductAccessor` only after successful validation.
4. An invalid ID becomes the public `PRODUCT_VALIDATION_FAILED` response with empty details while the internal `PRODUCT_ID_INVALID` reason is logged; an unknown canonical ID becomes `PRODUCT_NOT_FOUND`.
5. A known Product is mapped to the public response without exposing EF types.
