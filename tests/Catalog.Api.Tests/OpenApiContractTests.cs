using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace Catalog.Api.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    [Trait("Category", "OpenApi")]
    public async Task CatalogContractIsValidOpenApiAndMarksMigrationStatus()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "openapi", "catalog-api.yaml");
        var source = await File.ReadAllTextAsync(path, CancellationToken.None);
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();

        var result = await OpenApiDocument.LoadAsync(
            path,
            settings,
            CancellationToken.None);

        Assert.NotNull(result.Document);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Diagnostic.Errors);
        Assert.Equal(
            3,
            source.Split("x-implementation-status: implemented", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            4,
            source.Split("x-implementation-status: pending-dotnet", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "x-implementation-status: implemented",
            GetOperationBlock(source, "createProduct"),
            StringComparison.Ordinal);
        Assert.Contains(
            "x-implementation-status: implemented",
            GetOperationBlock(source, "getProduct"),
            StringComparison.Ordinal);
        Assert.Contains(
            "x-implementation-status: pending-dotnet",
            GetOperationBlock(source, "listProducts"),
            StringComparison.Ordinal);
        Assert.Contains("Location:", GetOperationBlock(source, "createProduct"), StringComparison.Ordinal);
        Assert.Contains("PRODUCT_VALIDATION_FAILED", source, StringComparison.Ordinal);
        Assert.Contains("required: [code, message, details]", source, StringComparison.Ordinal);
        Assert.Contains("^PRODUCT-[0-9a-fA-F]", source, StringComparison.Ordinal);
    }

    private static string GetOperationBlock(string source, string operationId)
    {
        var marker = $"      operationId: {operationId}";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Operation {operationId} must be documented.");
        var next = source.IndexOf("      operationId:", start + marker.Length, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }
}
