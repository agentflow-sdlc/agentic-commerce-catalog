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
        Assert.Contains("x-implementation-status: pending-dotnet", source, StringComparison.Ordinal);
    }
}
