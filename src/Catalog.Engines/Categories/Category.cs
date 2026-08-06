namespace Catalog.Engines.Categories;

public sealed record Category(
    string Id,
    string Name,
    string NormalizedName,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
