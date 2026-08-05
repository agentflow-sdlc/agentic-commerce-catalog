using System.Text.Json;
using Catalog.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Api.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnexpectedErrorsDoNotExposeExceptionDetailsOrStackTraces()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("sensitive failure detail"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(responseBody);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(
            "UNEXPECTED_ERROR",
            json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("correlationId").GetString()));
        Assert.DoesNotContain("sensitive failure detail", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", responseBody, StringComparison.OrdinalIgnoreCase);
    }
}
