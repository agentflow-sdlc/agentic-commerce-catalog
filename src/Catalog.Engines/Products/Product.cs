namespace Catalog.Engines.Products;

public sealed record Product(
    string Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
