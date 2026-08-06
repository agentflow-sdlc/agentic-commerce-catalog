namespace Catalog.Engines.Products;

public sealed record Product(
    string Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? CategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
