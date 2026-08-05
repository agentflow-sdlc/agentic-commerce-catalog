using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Catalog.Contracts;
using Catalog.Engines.Products;
using Catalog.Managers.Products;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Catalog.Api.Tests;

public sealed class ProductEndpointTests : IClassFixture<ProductApiFactory>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ProductEndpointTests(ProductApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostAndGetProductReturnStableCamelCaseContracts()
    {
        var sku = $"sku-{Guid.NewGuid():N}";
        using var postResponse = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(sku, " Product name ", " Description ", 10.50m),
            CancellationToken.None);
        var createdJson = await postResponse.Content.ReadAsStringAsync(CancellationToken.None);
        var created = JsonSerializer.Deserialize<ProductResponseEnvelope>(createdJson, SerializerOptions);
        using var document = JsonDocument.Parse(createdJson);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(sku.ToUpperInvariant(), created.Data.Sku);
        Assert.Equal("Product name", created.Data.Name);
        Assert.True(created.Data.IsActive);
        Assert.Equal(created.Data.CreatedAt, created.Data.UpdatedAt);
        Assert.True(document.RootElement.GetProperty("data").TryGetProperty("createdAt", out _));
        Assert.False(document.RootElement.GetProperty("data").TryGetProperty("CreatedAt", out _));
        Assert.Equal($"/products/{created.Data.Id}", postResponse.Headers.Location?.OriginalString);

        using var getResponse = await _client.GetAsync(
            $"/products/{created.Data.Id}",
            CancellationToken.None);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fetched);
        Assert.Equal(created.Data, fetched.Data);
    }

    [Fact]
    public async Task PostRejectsNegativePriceWithStableError()
    {
        using var response = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest("SKU-NEGATIVE", "Invalid", null, -0.01m),
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "PRODUCT_PRICE_INVALID",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostRejectsMalformedJsonWithStableError()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(
            "/products",
            content,
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "REQUEST_VALIDATION_FAILED",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostRejectsDuplicateNormalizedSkuWithStableConflict()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        using var firstResponse = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(sku, "First", null, 1m),
            CancellationToken.None);
        using var duplicateResponse = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest($" {sku.ToLowerInvariant()} ", "Second", null, 2m),
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await duplicateResponse.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(
            "PRODUCT_SKU_ALREADY_EXISTS",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetUnknownProductReturnsStableNotFoundError()
    {
        using var response = await _client.GetAsync(
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
}

public sealed class ProductApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProductAccessor>();
            services.RemoveAll<IProductIdGenerator>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IProductAccessor, TestProductAccessor>();
            services.AddSingleton<IProductIdGenerator, TestProductIdGenerator>();
            services.AddSingleton<TimeProvider>(new TestTimeProvider());
        });
    }

    private sealed class TestProductAccessor : IProductAccessor
    {
        private readonly Dictionary<string, Product> _products = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        public Task<bool> ExistsBySkuAsync(
            string normalizedSku,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                return Task.FromResult(
                    _products.Values.Any(product => product.Sku == normalizedSku));
            }
        }

        public Task<Product?> FindByIdAsync(string id, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _products.TryGetValue(id, out var product);
                return Task.FromResult(product);
            }
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _products.Add(product.Id, product);
                return Task.CompletedTask;
            }
        }
    }

    private sealed class TestProductIdGenerator : IProductIdGenerator
    {
        public string NewId() => $"PRODUCT-{Guid.NewGuid()}";
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 5, 17, 30, 0, TimeSpan.Zero);
    }
}
