namespace Catalog.Api.Middleware;

internal static partial class CatalogApiLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Catalog request completed: {Method} {Path} returned {StatusCode} in {DurationMs} ms")]
    public static partial void RequestCompleted(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        double durationMs);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Unhandled Catalog request error for correlation ID {CorrelationId} at {Path}")]
    public static partial void UnexpectedError(
        ILogger logger,
        Exception exception,
        string correlationId,
        string path);
}
