using Catalog.Engines.Products;

namespace Catalog.Product.Tests;

public sealed class ProductEngineTests
{
    private const string ProductId = "PRODUCT-00000000-0000-4000-8000-000000000001";

    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 5, 12, 30, 0, TimeSpan.FromHours(-5));

    private readonly ProductEngine _engine = new();

    [Fact]
    public void CreateNormalizesInputAndAppliesDeterministicDefaults()
    {
        var product = _engine.Create(
            $" {ProductId} ",
            " sku-001 ",
            " Product name ",
            " Product description ",
            10.50m,
            " CATEGORY-00000000-0000-4000-8000-000000000001 ",
            Timestamp);

        Assert.Equal(ProductId, product.Id);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Product name", product.Name);
        Assert.Equal("Product description", product.Description);
        Assert.Equal(10.50m, product.Price);
        Assert.Equal("CATEGORY-00000000-0000-4000-8000-000000000001", product.CategoryId);
        Assert.True(product.IsActive);
        Assert.Equal(Timestamp.ToUniversalTime(), product.CreatedAt);
        Assert.Equal(product.CreatedAt, product.UpdatedAt);
    }

    [Fact]
    public void CreateAcceptsZeroPriceAndNormalizesEmptyDescriptionToNull()
    {
        var product = _engine.Create(
            ProductId,
            "SKU-002",
            "Free product",
            "   ",
            0m,
            null,
            Timestamp);

        Assert.Equal(0m, product.Price);
        Assert.Null(product.Description);
    }

    [Fact]
    public void CreateRejectsPriceThatCannotBeRepresentedByDecimal18Scale2()
    {
        var precisionException = Assert.Throws<ProductValidationException>(() => _engine.Create(
            ProductId,
            "SKU-PRECISION",
            "Product",
            null,
            1.001m,
            null,
            Timestamp));
        var rangeException = Assert.Throws<ProductValidationException>(() => _engine.Create(
            ProductId,
            "SKU-RANGE",
            "Product",
            null,
            ProductEngine.MaxPrice + 0.01m,
            null,
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
            ProductId,
            sku,
            name,
            null,
            price is null ? null : price.Value,
            null,
            Timestamp));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Theory]
    [InlineData("PRODUCT-NOT-A-GUID")]
    [InlineData("WRONG-00000000-0000-4000-8000-000000000001")]
    [InlineData("product-00000000-0000-4000-8000-000000000001")]
    [InlineData("")]
    public void NormalizeIdRejectsInvalidStableIdentifiers(string id)
    {
        var exception = Assert.Throws<ProductValidationException>(() => _engine.NormalizeId(id));

        Assert.Equal("PRODUCT_ID_INVALID", exception.Code);
    }

    [Fact]
    public void CreateRejectsAnEmptyCategoryIdButAllowsNoCategory()
    {
        var withoutCategory = _engine.Create(
            ProductId,
            "SKU-NO-CATEGORY",
            "Product",
            null,
            1m,
            null,
            Timestamp);
        var exception = Assert.Throws<ProductValidationException>(() => _engine.Create(
            ProductId,
            "SKU-EMPTY-CATEGORY",
            "Product",
            null,
            1m,
            " ",
            Timestamp));

        Assert.Null(withoutCategory.CategoryId);
        Assert.Equal("PRODUCT_CATEGORY_ID_INVALID", exception.Code);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ChangeStatusPreservesProductDataAndUsesTheProvidedTimestamp(
        bool initialState,
        bool requestedState)
    {
        var product = _engine.Create(
            ProductId,
            "SKU-STATUS",
            "Product",
            null,
            1m,
            null,
            Timestamp) with
        { IsActive = initialState };
        var changedAt = Timestamp.AddHours(1);

        var updated = _engine.ChangeStatus(product, requestedState, changedAt);

        Assert.Equal(requestedState, updated.IsActive);
        Assert.Equal(changedAt.ToUniversalTime(), updated.UpdatedAt);
        Assert.Equal(product with { IsActive = requestedState, UpdatedAt = updated.UpdatedAt }, updated);
    }

    [Fact]
    public void ChangeStatusRejectsAMissingState()
    {
        var product = _engine.Create(
            ProductId,
            "SKU-STATUS",
            "Product",
            null,
            1m,
            null,
            Timestamp);

        var exception = Assert.Throws<ProductValidationException>(() =>
            _engine.ChangeStatus(product, null, Timestamp));

        Assert.Equal("PRODUCT_STATUS_REQUIRED", exception.Code);
    }
}
