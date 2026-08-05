using System.Net;
using System.Text.Json;
using Catalog.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Catalog.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealthReturnsExpectedJson()
    {
        using var response = await _client.GetAsync("/health", CancellationToken.None);
        var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var document = JsonDocument.Parse(responseJson);
        var body = JsonSerializer.Deserialize<HealthResponse>(
            responseJson,
            SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(3, document.RootElement.EnumerateObject().Count());
        Assert.True(document.RootElement.TryGetProperty("status", out _));
        Assert.False(document.RootElement.TryGetProperty("Status", out _));
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
        Assert.Equal("catalog-api", body.Service);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
    }

    [Fact]
    public async Task GetHealthPropagatesAValidCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "catalog-test-123");

        using var response = await _client.SendAsync(
            request,
            CancellationToken.None);

        Assert.Equal("catalog-test-123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task GetHealthGeneratesACorrelationIdWhenMissing()
    {
        using var response = await _client.GetAsync("/health", CancellationToken.None);

        var correlationId = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task UnknownRouteReturnsStructuredNotFoundResponse()
    {
        using var response = await _client.GetAsync(
            "/not-an-endpoint",
            CancellationToken.None);
        var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ROUTE_NOT_FOUND", body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            response.Headers.GetValues("X-Correlation-ID").Single(),
            body.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task DevelopmentOpenApiContainsOnlyImplementedEndpoints()
    {
        using var response = await _client.GetAsync(
            "/openapi/v1.json",
            CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/health", body, StringComparison.Ordinal);
        Assert.Contains("/products", body, StringComparison.Ordinal);
        Assert.DoesNotContain("/categories", body, StringComparison.Ordinal);
    }
}
