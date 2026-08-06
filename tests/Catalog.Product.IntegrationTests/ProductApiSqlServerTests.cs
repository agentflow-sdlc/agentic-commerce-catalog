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
    public async Task HealthRemainsAvailableWithTheSqlBackedApplicationHost()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "sql-health-test");
        using var response = await fixture.Client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sql-health-test", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task PostThenGetProductTraversesTheCompleteVerticalSlice()
    {
        var sku = $"sku-{Guid.NewGuid():N}";
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/products")
        {
            Content = JsonContent.Create(
                new CreateProductRequest(sku, " Product name ", " Description ", 10.50m)),
        };
        postRequest.Headers.Add("X-Correlation-ID", "sql-product-test");
        using var postResponse = await fixture.Client.SendAsync(postRequest, CancellationToken.None);
        var createdJson = await postResponse.Content.ReadAsStringAsync(CancellationToken.None);
        var created = JsonSerializer.Deserialize<ProductResponseEnvelope>(createdJson, SerializerOptions);
        using var createdDocument = JsonDocument.Parse(createdJson);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(sku.ToUpperInvariant(), created.Data.Sku);
        Assert.Equal("Product name", created.Data.Name);
        Assert.True(created.Data.IsActive);
        Assert.Equal(created.Data.CreatedAt, created.Data.UpdatedAt);
        Assert.Equal($"/products/{created.Data.Id}", postResponse.Headers.Location?.OriginalString);
        Assert.Equal("sql-product-test", postResponse.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("sql-product-test", created.CorrelationId);
        Assert.True(createdDocument.RootElement.GetProperty("data").TryGetProperty("createdAt", out _));
        Assert.False(createdDocument.RootElement.GetProperty("data").TryGetProperty("CreatedAt", out _));

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
    public async Task PostRejectsInvalidProductFieldsWithoutWritingThem()
    {
        var cases = new[]
        {
            new CreateProductRequest(" ", "Product", null, 1m),
            new CreateProductRequest($"SKU-{Guid.NewGuid():N}", " ", null, 1m),
            new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Product", null, -0.01m),
        };

        foreach (var testCase in cases)
        {
            using var response = await fixture.Client.PostAsJsonAsync(
                "/products",
                testCase,
                CancellationToken.None);
            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "PRODUCT_VALIDATION_FAILED",
                body.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.Empty(body.RootElement.GetProperty("error").GetProperty("details").EnumerateObject());
        }
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
            "/products/PRODUCT-00000000-0000-4000-8000-000000000099",
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
    public async Task GetRejectsInvalidProductIdFormat()
    {
        using var response = await fixture.Client.GetAsync(
            "/products/PRODUCT-NOT-A-GUID",
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "PRODUCT_VALIDATION_FAILED",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(body.RootElement.GetProperty("error").GetProperty("details").EnumerateObject());
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
