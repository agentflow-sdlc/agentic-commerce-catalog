namespace Catalog.Contracts;

public sealed record ProductResponse(
    string Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? CategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProductResponseEnvelope(ProductResponse Data, string CorrelationId);

public sealed record ProductListResponseEnvelope(
    IReadOnlyList<ProductResponse> Data,
    string CorrelationId);
