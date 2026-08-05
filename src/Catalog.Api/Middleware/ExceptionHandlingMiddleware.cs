using Catalog.Contracts;

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
            CatalogApiLog.UnexpectedError(
                _logger,
                exception,
                correlationId,
                context.Request.Path.Value ?? string.Empty);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            context.Response.Headers.CacheControl = "no-store";

            await context.Response.WriteAsJsonAsync(
                new ErrorResponse(
                    new ErrorDetail(
                        "UNEXPECTED_ERROR",
                        "An unexpected error occurred."),
                    correlationId),
                context.RequestAborted);
        }
    }
}
