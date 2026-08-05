namespace Catalog.Engines.Products;

public sealed class ProductValidationException : Exception
{
    public ProductValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class ProductSkuAlreadyExistsException : Exception
{
    public ProductSkuAlreadyExistsException(string sku, Exception? innerException = null)
        : base("A product with this SKU already exists.", innerException)
    {
        Sku = sku;
    }

    public string Sku { get; }
}
