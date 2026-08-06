using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Catalog.Contracts;
using Catalog.Engines.Categories;
using Catalog.Engines.Products;
using Catalog.Managers.Categories;
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
        Assert.Null(created.Data.CategoryId);
        Assert.True(created.Data.IsActive);
        Assert.Equal(created.Data.CreatedAt, created.Data.UpdatedAt);
        Assert.Equal(
            postResponse.Headers.GetValues("X-Correlation-ID").Single(),
            created.CorrelationId);
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
    public async Task ProductCanReferenceAnExistingCategoryAndListsWithTheSameContract()
    {
        using var categoryResponse = await _client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest($"Category {Guid.NewGuid():N}"),
            CancellationToken.None);
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        Assert.NotNull(category);

        using var productResponse = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(
                $"SKU-{Guid.NewGuid():N}",
                "Categorized product",
                null,
                5m,
                category.Data.Id),
            CancellationToken.None);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);
        using var listResponse = await _client.GetAsync("/products", CancellationToken.None);
        var products = await listResponse.Content.ReadFromJsonAsync<ProductListResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        Assert.NotNull(product);
        Assert.Equal(category.Data.Id, product.Data.CategoryId);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(products);
        Assert.Contains(products.Data, item => item == product.Data);
    }

    [Fact]
    public async Task PostRejectsAnUnknownCategory()
    {
        using var response = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(
                $"SKU-{Guid.NewGuid():N}",
                "Unknown category",
                null,
                5m,
                "CATEGORY-00000000-0000-4000-8000-000000000099"),
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "CATEGORY_NOT_FOUND",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchChangesProductStatusAndRejectsAMissingState()
    {
        using var createResponse = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Status product", null, 1m),
            CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);
        Assert.NotNull(created);

        using var deactivateResponse = await _client.PatchAsJsonAsync(
            $"/products/{created.Data.Id}/status",
            new UpdateProductStatusRequest(false),
            CancellationToken.None);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);
        using var invalidResponse = await _client.PatchAsJsonAsync(
            $"/products/{created.Data.Id}/status",
            new { },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.NotNull(deactivated);
        Assert.False(deactivated.Data.IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
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
            "PRODUCT_VALIDATION_FAILED",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(body.RootElement.GetProperty("error").GetProperty("details").EnumerateObject());
    }

    [Theory]
    [InlineData(" ", "Product")]
    [InlineData("SKU-REQUIRED-TEXT", " ")]
    public async Task PostRejectsMissingRequiredProductText(string sku, string name)
    {
        using var response = await _client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(sku, name, null, 1m),
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "PRODUCT_VALIDATION_FAILED",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(body.RootElement.GetProperty("error").GetProperty("details").EnumerateObject());
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
        Assert.Empty(body.RootElement.GetProperty("error").GetProperty("details").EnumerateObject());
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
        Assert.Equal(
            sku.ToUpperInvariant(),
            body.RootElement.GetProperty("error").GetProperty("details").GetProperty("sku").GetString());
    }

    [Fact]
    public async Task GetUnknownProductReturnsStableNotFoundError()
    {
        using var response = await _client.GetAsync(
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

    [Fact]
    public async Task GetRejectsInvalidProductIdBeforeTheUseCaseQueriesPersistence()
    {
        using var response = await _client.GetAsync(
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
}

public sealed class ProductApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProductAccessor>();
            services.RemoveAll<ICategoryAccessor>();
            services.RemoveAll<IProductIdGenerator>();
            services.RemoveAll<ICategoryIdGenerator>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IProductAccessor, TestProductAccessor>();
            services.AddSingleton<ICategoryAccessor, TestCategoryAccessor>();
            services.AddSingleton<IProductIdGenerator, TestProductIdGenerator>();
            services.AddSingleton<ICategoryIdGenerator, TestCategoryIdGenerator>();
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

        public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                IReadOnlyList<Product> products = _products.Values
                    .OrderByDescending(product => product.CreatedAt)
                    .ThenByDescending(product => product.Id, StringComparer.Ordinal)
                    .ToArray();
                return Task.FromResult(products);
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

        public Task<bool> UpdateStatusAsync(
            string id,
            bool isActive,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (!_products.TryGetValue(id, out var product))
                {
                    return Task.FromResult(false);
                }

                _products[id] = product with { IsActive = isActive, UpdatedAt = updatedAt };
                return Task.FromResult(true);
            }
        }
    }

    private sealed class TestCategoryAccessor : ICategoryAccessor
    {
        private readonly Dictionary<string, Category> _categories = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        public Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                return Task.FromResult(_categories.ContainsKey(id));
            }
        }

        public Task<bool> ExistsByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                return Task.FromResult(
                    _categories.Values.Any(category => category.NormalizedName == normalizedName));
            }
        }

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _categories.Add(category.Id, category);
                return Task.CompletedTask;
            }
        }

        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                IReadOnlyList<Category> categories = _categories.Values
                    .OrderBy(category => category.NormalizedName, StringComparer.Ordinal)
                    .ThenBy(category => category.Id, StringComparer.Ordinal)
                    .ToArray();
                return Task.FromResult(categories);
            }
        }
    }

    private sealed class TestProductIdGenerator : IProductIdGenerator
    {
        public string NewId() => $"PRODUCT-{Guid.NewGuid()}";
    }

    private sealed class TestCategoryIdGenerator : ICategoryIdGenerator
    {
        public string NewId() => $"CATEGORY-{Guid.NewGuid()}";
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 5, 17, 30, 0, TimeSpan.Zero);
    }
}
