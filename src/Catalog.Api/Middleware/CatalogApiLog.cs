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

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Starting Create Product for correlation ID {CorrelationId}")]
    public static partial void ProductCreateStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Information,
        Message = "Product {ProductId} created for correlation ID {CorrelationId}")]
    public static partial void ProductCreated(
        ILogger logger,
        string productId,
        string correlationId);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "Starting Get Product {ProductId} for correlation ID {CorrelationId}")]
    public static partial void ProductGetStarted(
        ILogger logger,
        string productId,
        string correlationId);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "Product {ProductId} retrieved for correlation ID {CorrelationId}")]
    public static partial void ProductRetrieved(
        ILogger logger,
        string productId,
        string correlationId);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Warning,
        Message = "Product validation failed with reason {ReasonCode} for correlation ID {CorrelationId}")]
    public static partial void ProductValidationFailed(
        ILogger logger,
        string reasonCode,
        string correlationId);

    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Warning,
        Message = "Product SKU {Sku} already exists for correlation ID {CorrelationId}")]
    public static partial void ProductSkuConflict(
        ILogger logger,
        string sku,
        string correlationId);

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Warning,
        Message = "Product {ProductId} was not found for correlation ID {CorrelationId}")]
    public static partial void ProductNotFound(
        ILogger logger,
        string productId,
        string correlationId);

    [LoggerMessage(
        EventId = 17,
        Level = LogLevel.Warning,
        Message = "Product request syntax is invalid for correlation ID {CorrelationId}")]
    public static partial void ProductRequestSyntaxInvalid(ILogger logger, string correlationId);
}
