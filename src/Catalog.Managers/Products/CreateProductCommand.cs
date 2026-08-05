namespace Catalog.Managers.Products;

public sealed record CreateProductCommand(
    string? Sku,
    string? Name,
    string? Description,
    decimal? Price);
