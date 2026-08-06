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
        Message = "Request syntax is invalid for correlation ID {CorrelationId}")]
    public static partial void RequestSyntaxInvalid(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Starting List Products for correlation ID {CorrelationId}")]
    public static partial void ProductListStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Information,
        Message = "Listed {ProductCount} products for correlation ID {CorrelationId}")]
    public static partial void ProductsListed(
        ILogger logger,
        int productCount,
        string correlationId);

    [LoggerMessage(
        EventId = 22,
        Level = LogLevel.Information,
        Message = "Starting status change for Product {ProductId} and correlation ID {CorrelationId}")]
    public static partial void ProductStatusChangeStarted(
        ILogger logger,
        string productId,
        string correlationId);

    [LoggerMessage(
        EventId = 23,
        Level = LogLevel.Information,
        Message = "Product {ProductId} status changed to {IsActive} for correlation ID {CorrelationId}")]
    public static partial void ProductStatusChanged(
        ILogger logger,
        string productId,
        bool isActive,
        string correlationId);

    [LoggerMessage(
        EventId = 24,
        Level = LogLevel.Information,
        Message = "Product {ProductId} associated with Category {CategoryId} for correlation ID {CorrelationId}")]
    public static partial void ProductCategoryAssociated(
        ILogger logger,
        string productId,
        string categoryId,
        string correlationId);

    [LoggerMessage(
        EventId = 30,
        Level = LogLevel.Information,
        Message = "Starting Create Category for correlation ID {CorrelationId}")]
    public static partial void CategoryCreateStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 31,
        Level = LogLevel.Information,
        Message = "Category {CategoryId} created for correlation ID {CorrelationId}")]
    public static partial void CategoryCreated(
        ILogger logger,
        string categoryId,
        string correlationId);

    [LoggerMessage(
        EventId = 32,
        Level = LogLevel.Information,
        Message = "Starting List Categories for correlation ID {CorrelationId}")]
    public static partial void CategoryListStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 33,
        Level = LogLevel.Information,
        Message = "Listed {CategoryCount} categories for correlation ID {CorrelationId}")]
    public static partial void CategoriesListed(
        ILogger logger,
        int categoryCount,
        string correlationId);

    [LoggerMessage(
        EventId = 34,
        Level = LogLevel.Warning,
        Message = "Category validation failed with reason {ReasonCode} for correlation ID {CorrelationId}")]
    public static partial void CategoryValidationFailed(
        ILogger logger,
        string reasonCode,
        string correlationId);

    [LoggerMessage(
        EventId = 35,
        Level = LogLevel.Warning,
        Message = "Category name {NormalizedName} already exists for correlation ID {CorrelationId}")]
    public static partial void CategoryNameConflict(
        ILogger logger,
        string normalizedName,
        string correlationId);

    [LoggerMessage(
        EventId = 36,
        Level = LogLevel.Warning,
        Message = "Category {CategoryId} was not found for correlation ID {CorrelationId}")]
    public static partial void CategoryNotFound(
        ILogger logger,
        string categoryId,
        string correlationId);
}
