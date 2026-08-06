namespace Catalog.Accessors.Sql;

internal sealed class CategoryEntity
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
