namespace Catalog.Accessors.Sql;

internal sealed class ProductEntity
{
    public required string Id { get; set; }

    public required string Sku { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
