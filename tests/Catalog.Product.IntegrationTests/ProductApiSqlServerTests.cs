using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.Accessors.Sql;
using Catalog.Contracts;
using Catalog.Engines.Categories;
using Catalog.Engines.Products;
using Microsoft.EntityFrameworkCore;
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
        await fixture.ResetAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "sql-health-test");
        using var response = await fixture.Client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sql-health-test", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task CompleteMigrationChainAppliesAndEmptyCollectionsReturnEmptyArrays()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var migrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();

        using var productsResponse = await fixture.Client.GetAsync("/products", CancellationToken.None);
        var products = await productsResponse.Content.ReadFromJsonAsync<ProductListResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);
        using var categoriesResponse = await fixture.Client.GetAsync("/categories", CancellationToken.None);
        var categories = await categoriesResponse.Content.ReadFromJsonAsync<CategoryListResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(2, migrations.Length);
        Assert.Contains(migrations, migration => migration.EndsWith("_InitialProduct", StringComparison.Ordinal));
        Assert.Contains(
            migrations,
            migration => migration.EndsWith("_AddCategoriesAndProductCategory", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
        Assert.NotNull(products);
        Assert.Empty(products.Data);
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);
        Assert.NotNull(categories);
        Assert.Empty(categories.Data);
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task ProductWithoutCategoryPersistsAcrossPostGetAndList()
    {
        await fixture.ResetAsync();
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
        Assert.Null(created.Data.CategoryId);
        Assert.True(created.Data.IsActive);
        Assert.Equal($"/products/{created.Data.Id}", postResponse.Headers.Location?.OriginalString);
        Assert.Equal("sql-product-test", created.CorrelationId);
        Assert.True(createdDocument.RootElement.GetProperty("data").TryGetProperty("createdAt", out _));
        Assert.False(createdDocument.RootElement.GetProperty("data").TryGetProperty("CreatedAt", out _));

        using var getResponse = await fixture.Client.GetAsync(
            $"/products/{created.Data.Id}",
            CancellationToken.None);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);
        using var listResponse = await fixture.Client.GetAsync("/products", CancellationToken.None);
        var listed = await listResponse.Content.ReadFromJsonAsync<ProductListResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(created.Data, fetched.Data);
        Assert.NotNull(listed);
        Assert.Equal(created.Data, Assert.Single(listed.Data));
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task CategoryCreationNormalizationAndListingPersistAcrossRequests()
    {
        await fixture.ResetAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = JsonContent.Create(
                new CreateCategoryRequest("  Home   Appliances ", " Description ")),
        };
        request.Headers.Add("X-Correlation-ID", "sql-category-test");
        using var createResponse = await fixture.Client.SendAsync(request, CancellationToken.None);
        var json = await createResponse.Content.ReadAsStringAsync(CancellationToken.None);
        var created = JsonSerializer.Deserialize<CategoryResponseEnvelope>(json, SerializerOptions);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Null(createResponse.Headers.Location);
        Assert.NotNull(created);
        Assert.Equal("Home Appliances", created.Data.Name);
        Assert.Equal("Description", created.Data.Description);
        Assert.Equal("sql-category-test", created.CorrelationId);
        Assert.False(document.RootElement.GetProperty("data").TryGetProperty("normalizedName", out _));

        using var listResponse = await fixture.Client.GetAsync("/categories", CancellationToken.None);
        var listed = await listResponse.Content.ReadFromJsonAsync<CategoryListResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.NotNull(listed);
        Assert.Equal(created.Data, Assert.Single(listed.Data));
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task ProductCanReferenceAnExistingCategoryAndRetainsTheForeignKey()
    {
        await fixture.ResetAsync();
        var category = await CreateCategoryAsync($"Category {Guid.NewGuid():N}");
        var product = await CreateProductAsync(
            new CreateProductRequest(
                $"SKU-{Guid.NewGuid():N}",
                "Categorized product",
                null,
                5m,
                category.Id));

        using var response = await fixture.Client.GetAsync(
            $"/products/{product.Id}",
            CancellationToken.None);
        var fetched = await response.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(category.Id, product.CategoryId);
        Assert.Equal(category.Id, fetched.Data.CategoryId);
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task ProductCreationRejectsAnUnknownCategoryWithSafeError()
    {
        await fixture.ResetAsync();
        var categoryId = "CATEGORY-00000000-0000-4000-8000-000000000099";
        using var response = await fixture.Client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(
                $"SKU-{Guid.NewGuid():N}",
                "Unknown category",
                null,
                5m,
                categoryId),
            CancellationToken.None);
        var json = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var body = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "CATEGORY_NOT_FOUND",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            categoryId,
            body.RootElement.GetProperty("error").GetProperty("details").GetProperty("categoryId").GetString());
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task ProductStatusCanDeactivateActivateAndRepeatTheSameState()
    {
        await fixture.ResetAsync();
        var created = await CreateProductAsync(
            new CreateProductRequest($"SKU-{Guid.NewGuid():N}", "Status product", null, 1m));

        var deactivated = await SetStatusAsync(created.Id, false);
        var repeated = await SetStatusAsync(created.Id, false);
        var reactivated = await SetStatusAsync(created.Id, true);

        Assert.False(deactivated.IsActive);
        Assert.False(repeated.IsActive);
        Assert.True(repeated.UpdatedAt >= deactivated.UpdatedAt);
        Assert.True(reactivated.IsActive);
        Assert.Equal(created.Sku, reactivated.Sku);
        Assert.Equal(created.CategoryId, reactivated.CategoryId);
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task ProductAndCategoryValidationAndConflictsUseStableErrors()
    {
        await fixture.ResetAsync();
        using var invalidProduct = await fixture.Client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest(" ", "Product", null, -0.01m),
            CancellationToken.None);
        using var invalidCategory = await fixture.Client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest(" "),
            CancellationToken.None);
        var category = await CreateCategoryAsync("Home Appliances");
        using var duplicateCategory = await fixture.Client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest(" home   appliances "),
            CancellationToken.None);
        var sku = $"SKU-{Guid.NewGuid():N}";
        await CreateProductAsync(new CreateProductRequest(sku, "Product", null, 0m, category.Id));
        using var duplicateProduct = await fixture.Client.PostAsJsonAsync(
            "/products",
            new CreateProductRequest($" {sku.ToLowerInvariant()} ", "Duplicate", null, 1m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, invalidProduct.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateProduct.StatusCode);
        Assert.Equal("CATEGORY_NAME_ALREADY_EXISTS", await ReadErrorCodeAsync(duplicateCategory));
        Assert.Equal("PRODUCT_SKU_ALREADY_EXISTS", await ReadErrorCodeAsync(duplicateProduct));
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task UnknownAndInvalidProductIdsReturnStableErrors()
    {
        await fixture.ResetAsync();
        using var unknown = await fixture.Client.GetAsync(
            "/products/PRODUCT-00000000-0000-4000-8000-000000000099",
            CancellationToken.None);
        using var invalid = await fixture.Client.GetAsync(
            "/products/PRODUCT-NOT-A-GUID",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("PRODUCT_NOT_FOUND", await ReadErrorCodeAsync(unknown));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("PRODUCT_VALIDATION_FAILED", await ReadErrorCodeAsync(invalid));
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task DatabaseUniqueIndexesRejectWriteRacesForSkuAndCategoryName()
    {
        await fixture.ResetAsync();
        var timestamp = new DateTimeOffset(2026, 8, 6, 17, 30, 0, TimeSpan.Zero);
        var sku = $"SKU-{Guid.NewGuid():N}";
        var firstProduct = new DomainProduct(
            $"PRODUCT-{Guid.NewGuid()}", sku, "First", null, 1m, null, true, timestamp, timestamp);
        var secondProduct = new DomainProduct(
            $"PRODUCT-{Guid.NewGuid()}", sku, "Second", null, 2m, null, true, timestamp, timestamp);
        var firstCategory = new Category(
            $"CATEGORY-{Guid.NewGuid()}", "Books", "books", null, timestamp, timestamp);
        var secondCategory = new Category(
            $"CATEGORY-{Guid.NewGuid()}", "BOOKS", "books", null, timestamp, timestamp);

        using (var firstScope = fixture.Services.CreateScope())
        {
            await firstScope.ServiceProvider.GetRequiredService<IProductAccessor>()
                .AddAsync(firstProduct, CancellationToken.None);
            await firstScope.ServiceProvider.GetRequiredService<ICategoryAccessor>()
                .AddAsync(firstCategory, CancellationToken.None);
        }

        using (var secondProductScope = fixture.Services.CreateScope())
        {
            await Assert.ThrowsAsync<ProductSkuAlreadyExistsException>(() =>
                secondProductScope.ServiceProvider.GetRequiredService<IProductAccessor>()
                    .AddAsync(secondProduct, CancellationToken.None));
        }

        using var secondCategoryScope = fixture.Services.CreateScope();
        await Assert.ThrowsAsync<CategoryNameAlreadyExistsException>(() =>
            secondCategoryScope.ServiceProvider.GetRequiredService<ICategoryAccessor>()
                .AddAsync(secondCategory, CancellationToken.None));
    }

    [SqlServerFact]
    [Trait("Category", "SqlIntegration")]
    public async Task DatabaseForeignKeyRejectsAnUnknownCategory()
    {
        await fixture.ResetAsync();
        var timestamp = new DateTimeOffset(2026, 8, 6, 17, 30, 0, TimeSpan.Zero);
        var categoryId = "CATEGORY-00000000-0000-4000-8000-000000000099";
        var product = new DomainProduct(
            $"PRODUCT-{Guid.NewGuid()}",
            $"SKU-{Guid.NewGuid():N}",
            "Foreign key",
            null,
            1m,
            categoryId,
            true,
            timestamp,
            timestamp);

        using var scope = fixture.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IProductAccessor>();

        var exception = await Assert.ThrowsAsync<ProductCategoryNotFoundException>(() =>
            accessor.AddAsync(product, CancellationToken.None));

        Assert.Equal(categoryId, exception.CategoryId);
    }

    private async Task<CategoryResponse> CreateCategoryAsync(string name)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest(name),
            CancellationToken.None);
        var envelope = await response.Content.ReadFromJsonAsync<CategoryResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(envelope);
        return envelope.Data;
    }

    private async Task<ProductResponse> CreateProductAsync(CreateProductRequest request)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/products",
            request,
            CancellationToken.None);
        var envelope = await response.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(envelope);
        return envelope.Data;
    }

    private async Task<ProductResponse> SetStatusAsync(string productId, bool isActive)
    {
        using var response = await fixture.Client.PatchAsJsonAsync(
            $"/products/{productId}/status",
            new UpdateProductStatusRequest(isActive),
            CancellationToken.None);
        var envelope = await response.Content.ReadFromJsonAsync<ProductResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        return envelope.Data;
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));
        return body.RootElement.GetProperty("error").GetProperty("code").GetString();
    }
}
