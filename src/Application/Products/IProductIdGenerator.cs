namespace Catalog.Application.Products;

public interface IProductIdGenerator
{
    string NewId();
}

public sealed class ProductIdGenerator : IProductIdGenerator
{
    public string NewId() => $"PRODUCT-{Guid.NewGuid()}";
}
