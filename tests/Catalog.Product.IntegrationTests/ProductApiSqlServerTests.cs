using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.Contracts;
using Catalog.Engines.Products;
using Microsoft.Extensions.DependencyInjection;
using DomainProduct = Catalog.Engines.Products.Product;

namespace Catalog.Product.IntegrationTests;

public sealed class ProductApiSqlServerTests(SqlServerApiFixture fixture)
    : IClassFixture<SqlServerApiFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task PostThenGetProductTraversesTheCompleteVerticalSlice()
    {
        var sku = $"sku-{Guid.NewGuid():N}";
        using var postResponse = await fixture.Client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(sku, " Product name ", " Description ", 10.50m),
            CancellationToken.None);
        var created = await postResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(sku.ToUpperInvariant(), created.Data.Sku);
        Assert.Equal("Product name", created.Data.Name);
        Assert.True(created.Data.IsActive);
        Assert.Equal(created.Data.CreatedAt, created.Data.UpdatedAt);
        Assert.Equal($"/products/{created.Data.Id}", postResponse.Headers.Location?.OriginalString);

        using var getResponse = await fixture.Client.GetAsync(
            $"/products/{created.Data.Id}",
            CancellationToken.None);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fetched);
        Assert.Equal(created.Data, fetched.Data);
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task PostAcceptsZeroPriceAndRejectsDuplicateNormalizedSku()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        using var firstResponse = await fixture.Client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(sku, "Free product", null, 0m),
            CancellationToken.None);
        using var duplicateResponse = await fixture.Client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest($" {sku.ToLowerInvariant()} ", "Duplicate", null, 1m),
            CancellationToken.None);
        using var duplicateBody = JsonDocument.Parse(
            await duplicateResponse.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(
            "PRODUCT_SKU_ALREADY_EXISTS",
            duplicateBody.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task GetUnknownProductReturnsStableNotFoundError()
    {
        using var response = await fixture.Client.GetAsync(
            "/products/PRODUCT-UNKNOWN",
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "PRODUCT_NOT_FOUND",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            response.Headers.GetValues("X-Correlation-ID").Single(),
            body.RootElement.GetProperty("correlationId").GetString());
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task DatabaseUniqueIndexRejectsDuplicateSkuDuringAWriteRace()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var timestamp = new DateTimeOffset(2026, 8, 5, 17, 30, 0, TimeSpan.Zero);
        var first = new DomainProduct($"PRODUCT-{Guid.NewGuid()}", sku, "First", null, 1m, true, timestamp, timestamp);
        var second = new DomainProduct($"PRODUCT-{Guid.NewGuid()}", sku, "Second", null, 2m, true, timestamp, timestamp);

        using (var firstScope = fixture.Services.CreateScope())
        {
            var accessor = firstScope.ServiceProvider.GetRequiredService<IProductAccessor>();
            await accessor.AddAsync(first, CancellationToken.None);
        }

        using var secondScope = fixture.Services.CreateScope();
        var secondAccessor = secondScope.ServiceProvider.GetRequiredService<IProductAccessor>();
        await Assert.ThrowsAsync<ProductSkuAlreadyExistsException>(() =>
            secondAccessor.AddAsync(second, CancellationToken.None));
    }
}
