using System.Text.Json;
using Catalog.Api;
using Catalog.Api.Middleware;
using Catalog.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet(
        "/health",
        () => Results.Ok(
            new HealthResponse("healthy", ServiceMetadata.Name, ServiceMetadata.Version)))
    .WithName("GetHealth")
    .WithTags("Health")
    .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapFallback(
    (HttpContext context) => Results.Json(
        new ErrorResponse(
            new ErrorDetail("ROUTE_NOT_FOUND", "The requested route does not exist."),
            CorrelationIdMiddleware.GetCorrelationId(context)),
        statusCode: StatusCodes.Status404NotFound));

app.Run();

public partial class Program;
