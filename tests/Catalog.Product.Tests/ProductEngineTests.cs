using Catalog.Engines.Products;

namespace Catalog.Product.Tests;

public sealed class ProductEngineTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 5, 12, 30, 0, TimeSpan.FromHours(-5));

    private readonly ProductEngine _engine = new();

    [Fact]
    public void CreateNormalizesInputAndAppliesDeterministicDefaults()
    {
        var product = _engine.Create(
            " PRODUCT-1 ",
            " sku-001 ",
            " Product name ",
            " Product description ",
            10.50m,
            Timestamp);

        Assert.Equal("PRODUCT-1", product.Id);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Product name", product.Name);
        Assert.Equal("Product description", product.Description);
        Assert.Equal(10.50m, product.Price);
        Assert.True(product.IsActive);
        Assert.Equal(Timestamp.ToUniversalTime(), product.CreatedAt);
        Assert.Equal(product.CreatedAt, product.UpdatedAt);
    }

    [Fact]
    public void CreateAcceptsZeroPriceAndNormalizesEmptyDescriptionToNull()
    {
        var product = _engine.Create(
            "PRODUCT-2",
            "SKU-002",
            "Free product",
            "   ",
            0m,
            Timestamp);

        Assert.Equal(0m, product.Price);
        Assert.Null(product.Description);
    }

    [Fact]
    public void CreateRejectsPriceThatCannotBeRepresentedByDecimal18Scale2()
    {
        var precisionException = Assert.Throws<ProductValidationException>(() => _engine.Create(
            "PRODUCT-PRICE-PRECISION",
            "SKU-PRECISION",
            "Product",
            null,
            1.001m,
            Timestamp));
        var rangeException = Assert.Throws<ProductValidationException>(() => _engine.Create(
            "PRODUCT-PRICE-RANGE",
            "SKU-RANGE",
            "Product",
            null,
            ProductEngine.MaxPrice + 0.01m,
            Timestamp));

        Assert.Equal("PRODUCT_PRICE_PRECISION_INVALID", precisionException.Code);
        Assert.Equal("PRODUCT_PRICE_OUT_OF_RANGE", rangeException.Code);
    }

    [Theory]
    [InlineData(null, "Name", 1, "PRODUCT_SKU_REQUIRED")]
    [InlineData(" ", "Name", 1, "PRODUCT_SKU_REQUIRED")]
    [InlineData("SKU-1", null, 1, "PRODUCT_NAME_REQUIRED")]
    [InlineData("SKU-1", " ", 1, "PRODUCT_NAME_REQUIRED")]
    [InlineData("SKU-1", "Name", null, "PRODUCT_PRICE_REQUIRED")]
    [InlineData("SKU-1", "Name", -1, "PRODUCT_PRICE_INVALID")]
    public void CreateRejectsMissingOrInvalidRequiredValues(
        string? sku,
        string? name,
        int? price,
        string expectedCode)
    {
        var exception = Assert.Throws<ProductValidationException>(() => _engine.Create(
            "PRODUCT-3",
            sku,
            name,
            null,
            price is null ? null : price.Value,
            Timestamp));

        Assert.Equal(expectedCode, exception.Code);
    }
}
