namespace Catalog.Clients.Api.Contracts;

public sealed record CreateProductRequest(
    string? Sku,
    string? Name,
    string? Description,
    decimal? Price,
    string? CategoryId = null);
