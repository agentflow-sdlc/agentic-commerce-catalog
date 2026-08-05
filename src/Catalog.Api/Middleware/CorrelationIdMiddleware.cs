using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Catalog.Api.Middleware;

public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private const string ItemKey = "Catalog.CorrelationId";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var supplied = context.Request.Headers[HeaderName].ToString().Trim();
        var correlationId = CorrelationIdPattern().IsMatch(supplied)
            ? supplied
            : Guid.NewGuid().ToString();
        var startedAt = Stopwatch.GetTimestamp();

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = _logger.BeginScope(
            new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        try
        {
            await _next(context);
        }
        finally
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                CatalogApiLog.RequestCompleted(
                    _logger,
                    context.Request.Method,
                    context.Request.Path.Value ?? string.Empty,
                    context.Response.StatusCode,
                    durationMs);
            }
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(ItemKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}
