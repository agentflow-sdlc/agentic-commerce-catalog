namespace Catalog.Application.Products;

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException(string productId)
        : base("The requested product does not exist.")
    {
        ProductId = productId;
    }

    public string ProductId { get; }
}
