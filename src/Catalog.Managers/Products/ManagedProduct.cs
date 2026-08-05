namespace Catalog.Managers.Products;

public sealed record ManagedProduct(
    string Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
