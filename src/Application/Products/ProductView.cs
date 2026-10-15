using Catalog.Domain.Products;

namespace Catalog.Application.Products;

public sealed record ProductView(
    string Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? CategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static ProductView From(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Price,
        product.CategoryId,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}
