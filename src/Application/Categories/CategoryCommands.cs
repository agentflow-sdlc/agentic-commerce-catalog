using Catalog.Domain.Categories;

namespace Catalog.Application.Categories;

public sealed record CreateCategoryCommand(string? Name, string? Description = null);

public sealed record CategoryView(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static CategoryView From(Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.CreatedAt,
        category.UpdatedAt);
}
