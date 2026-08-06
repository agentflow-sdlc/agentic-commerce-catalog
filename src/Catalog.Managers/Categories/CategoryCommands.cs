namespace Catalog.Managers.Categories;

public sealed record CreateCategoryCommand(string? Name, string? Description = null);

public sealed record ManagedCategory(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
