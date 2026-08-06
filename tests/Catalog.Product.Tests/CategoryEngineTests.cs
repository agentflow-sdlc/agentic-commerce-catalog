using Catalog.Engines.Categories;

namespace Catalog.Product.Tests;

public sealed class CategoryEngineTests
{
    private const string CategoryId = "CATEGORY-00000000-0000-4000-8000-000000000001";
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 6, 12, 30, 0, TimeSpan.FromHours(-5));
    private readonly CategoryEngine _engine = new();

    [Fact]
    public void CreateNormalizesDisplayAndComparisonNamesAndUsesControlledTimestamps()
    {
        var category = _engine.Create(
            CategoryId,
            "  Home   Appliances ",
            " Description ",
            Timestamp);

        Assert.Equal(CategoryId, category.Id);
        Assert.Equal("Home Appliances", category.Name);
        Assert.Equal("home appliances", category.NormalizedName);
        Assert.Equal("Description", category.Description);
        Assert.Equal(Timestamp.ToUniversalTime(), category.CreatedAt);
        Assert.Equal(category.CreatedAt, category.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsMissingNames(string? name)
    {
        var exception = Assert.Throws<CategoryValidationException>(() =>
            _engine.Create(CategoryId, name, null, Timestamp));

        Assert.Equal("CATEGORY_NAME_REQUIRED", exception.Code);
    }

    [Fact]
    public void CreateNormalizesAnEmptyDescriptionToNull()
    {
        var category = _engine.Create(CategoryId, "Books", " ", Timestamp);

        Assert.Null(category.Description);
    }

    [Theory]
    [InlineData("CATEGORY-NOT-A-GUID")]
    [InlineData("category-00000000-0000-4000-8000-000000000001")]
    [InlineData("WRONG-00000000-0000-4000-8000-000000000001")]
    public void CreateRejectsInvalidStableIds(string id)
    {
        var exception = Assert.Throws<CategoryValidationException>(() =>
            _engine.Create(id, "Books", null, Timestamp));

        Assert.Equal("CATEGORY_ID_INVALID", exception.Code);
    }
}
