using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.Contracts;

namespace Catalog.Api.Tests;

public sealed class CategoryEndpointTests : IClassFixture<ProductApiFactory>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public CategoryEndpointTests(ProductApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCreatesNormalizedCategoryAndGetListsTheSameCamelCaseContract()
    {
        var name = $"  Home   Appliances {Guid.NewGuid():N} ";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = JsonContent.Create(new CreateCategoryRequest(name, " Description ")),
        };
        request.Headers.Add("X-Correlation-ID", "category-api-test");
        using var createResponse = await _client.SendAsync(request, CancellationToken.None);
        var json = await createResponse.Content.ReadAsStringAsync(CancellationToken.None);
        var created = JsonSerializer.Deserialize<CategoryResponseEnvelope>(json, SerializerOptions);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Null(createResponse.Headers.Location);
        Assert.NotNull(created);
        Assert.DoesNotContain("  ", created.Data.Name, StringComparison.Ordinal);
        Assert.Equal("Description", created.Data.Description);
        Assert.Equal("category-api-test", created.CorrelationId);
        Assert.True(document.RootElement.GetProperty("data").TryGetProperty("createdAt", out _));
        Assert.False(document.RootElement.GetProperty("data").TryGetProperty("normalizedName", out _));

        using var listResponse = await _client.GetAsync("/categories", CancellationToken.None);
        var categories = await listResponse.Content.ReadFromJsonAsync<CategoryListResponseEnvelope>(
            SerializerOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(categories);
        Assert.Contains(categories.Data, category => category == created.Data);
    }

    [Fact]
    public async Task PostRejectsMissingNameWithStableValidationError()
    {
        using var response = await _client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest(" "),
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "CATEGORY_VALIDATION_FAILED",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(body.RootElement.GetProperty("error").GetProperty("details").EnumerateObject());
    }

    [Fact]
    public async Task PostRejectsDuplicateNormalizedNameWithStableConflict()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var first = await _client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest($"Home   Appliances {suffix}"),
            CancellationToken.None);
        using var duplicate = await _client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest($" home appliances {suffix} ".ToUpperInvariant()),
            CancellationToken.None);
        using var body = JsonDocument.Parse(
            await duplicate.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            "CATEGORY_NAME_ALREADY_EXISTS",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
