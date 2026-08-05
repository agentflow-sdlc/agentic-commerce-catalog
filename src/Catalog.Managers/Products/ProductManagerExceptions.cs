namespace Catalog.Managers.Products;

public sealed class ProductRequestException : Exception
{
    public ProductRequestException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class ProductConflictException : Exception
{
    public ProductConflictException(string sku, Exception? innerException = null)
        : base("A product with this SKU already exists.", innerException)
    {
        Sku = sku;
    }

    public string Sku { get; }
}
