namespace Catalog.Managers.Products;

public sealed record ManagedProduct(
    string Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? CategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
