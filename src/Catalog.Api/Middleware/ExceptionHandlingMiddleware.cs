using Catalog.Contracts;
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
            var (statusCode, code, message, isExpected) = MapException(exception);

            if (!isExpected)
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
                    new ErrorDetail(code, message),
                    correlationId),
                context.RequestAborted);
        }
    }

    private static (int StatusCode, string Code, string Message, bool IsExpected) MapException(
        Exception exception) => exception switch
        {
            ProductRequestException validation => (
                    StatusCodes.Status400BadRequest,
                    validation.Code,
                    validation.Message,
                    true),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "REQUEST_VALIDATION_FAILED",
                "The request payload is invalid.",
                true),
            ProductNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "PRODUCT_NOT_FOUND",
                notFound.Message,
                true),
            ProductConflictException conflict => (
                    StatusCodes.Status409Conflict,
                    "PRODUCT_SKU_ALREADY_EXISTS",
                    conflict.Message,
                    true),
            _ => (
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected error occurred.",
                false),
        };
}
