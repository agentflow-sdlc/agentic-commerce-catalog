using System.Text.Json;
using Catalog.Accessors.Sql;
using Catalog.Api;
using Catalog.Api.Middleware;
using Catalog.Contracts;
using Catalog.Managers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddOpenApi();

var catalogDbConnectionString = builder.Configuration.GetConnectionString("CatalogDb");
if (string.IsNullOrWhiteSpace(catalogDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'CatalogDb' is required. Configure ConnectionStrings__CatalogDb or user secrets.");
}

builder.Services.AddCatalogAccessors(catalogDbConnectionString);
builder.Services.AddCatalogManagers();

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

app.MapProductEndpoints();

app.MapFallback(
    (HttpContext context) => Results.Json(
        new ErrorResponse(
            new ErrorDetail("ROUTE_NOT_FOUND", "The requested route does not exist."),
            CorrelationIdMiddleware.GetCorrelationId(context)),
        statusCode: StatusCodes.Status404NotFound));

app.Run();

public partial class Program;
