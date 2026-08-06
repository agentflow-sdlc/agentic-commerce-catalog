using Catalog.Contracts;
using Catalog.Managers.Categories;
using Catalog.Managers.Products;

namespace Catalog.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
            var (statusCode, code, message, details, isExpected) = MapException(exception);

            if (isExpected)
            {
                LogExpectedException(exception, correlationId);
            }
            else
            {
                CatalogApiLog.UnexpectedError(
                    _logger,
                    exception,
                    correlationId,
                    context.Request.Path.Value ?? string.Empty);
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
            context.Response.Headers.CacheControl = "no-store";

            await context.Response.WriteAsJsonAsync(
                new ErrorResponse(
                    new ErrorDetail(code, message, details),
                    correlationId),
                context.RequestAborted);
        }
    }

    private void LogExpectedException(Exception exception, string correlationId)
    {
        switch (exception)
        {
            case ProductRequestException validation:
                CatalogApiLog.ProductValidationFailed(_logger, validation.Code, correlationId);
                break;
            case ProductConflictException conflict:
                CatalogApiLog.ProductSkuConflict(_logger, conflict.Sku, correlationId);
                break;
            case ProductNotFoundException notFound:
                CatalogApiLog.ProductNotFound(_logger, notFound.ProductId, correlationId);
                break;
            case CategoryRequestException validation:
                CatalogApiLog.CategoryValidationFailed(_logger, validation.Code, correlationId);
                break;
            case CategoryConflictException conflict:
                CatalogApiLog.CategoryNameConflict(_logger, conflict.NormalizedName, correlationId);
                break;
            case CategoryNotFoundException notFound:
                CatalogApiLog.CategoryNotFound(_logger, notFound.CategoryId, correlationId);
                break;
            case BadHttpRequestException:
                CatalogApiLog.RequestSyntaxInvalid(_logger, correlationId);
                break;
        }
    }

    private static (
        int StatusCode,
        string Code,
        string Message,
        IReadOnlyDictionary<string, object?> Details,
        bool IsExpected) MapException(
        Exception exception) => exception switch
        {
            ProductRequestException validation => (
                StatusCodes.Status400BadRequest,
                "PRODUCT_VALIDATION_FAILED",
                "The product request is invalid.",
                new Dictionary<string, object?>(),
                true),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "REQUEST_VALIDATION_FAILED",
                "The request payload is invalid.",
                new Dictionary<string, object?>(),
                true),
            ProductNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "PRODUCT_NOT_FOUND",
                notFound.Message,
                new Dictionary<string, object?> { ["productId"] = notFound.ProductId },
                true),
            ProductConflictException conflict => (
                StatusCodes.Status409Conflict,
                "PRODUCT_SKU_ALREADY_EXISTS",
                conflict.Message,
                new Dictionary<string, object?> { ["sku"] = conflict.Sku },
                true),
            CategoryRequestException => (
                StatusCodes.Status400BadRequest,
                "CATEGORY_VALIDATION_FAILED",
                "The category request is invalid.",
                new Dictionary<string, object?>(),
                true),
            CategoryConflictException conflict => (
                StatusCodes.Status409Conflict,
                "CATEGORY_NAME_ALREADY_EXISTS",
                conflict.Message,
                new Dictionary<string, object?> { ["normalizedName"] = conflict.NormalizedName },
                true),
            CategoryNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "CATEGORY_NOT_FOUND",
                notFound.Message,
                new Dictionary<string, object?> { ["categoryId"] = notFound.CategoryId },
                true),
            _ => (
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected error occurred.",
                new Dictionary<string, object?>(),
                false),
        };
}
