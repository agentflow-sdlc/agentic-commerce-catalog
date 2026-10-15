using System.Text.Json;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Catalog.Application;
using Catalog.Clients.Api;
using Catalog.Clients.Api.Contracts;
using Catalog.Clients.Api.Middleware;
using Catalog.Infrastructure.Sql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddOpenApi();

var applicationInsightsConnectionString =
    builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services
        .AddOpenTelemetry()
        .UseAzureMonitor(options =>
        {
            options.ConnectionString = applicationInsightsConnectionString;
            options.Credential = new DefaultAzureCredential();
        });
}

var catalogDbConnectionString = builder.Configuration.GetConnectionString("CatalogDb");
if (string.IsNullOrWhiteSpace(catalogDbConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'CatalogDb' is required. Configure ConnectionStrings__CatalogDb or user secrets.");
}

builder.Services.AddCatalogSql(catalogDbConnectionString);
builder.Services.AddCatalogApplication();

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
app.MapCategoryEndpoints();

app.MapFallback(
    (HttpContext context) => Results.Json(
        new ErrorResponse(
            new ErrorDetail(
                "ROUTE_NOT_FOUND",
                "The requested route does not exist.",
                new Dictionary<string, object?>()),
            CorrelationIdMiddleware.GetCorrelationId(context)),
        statusCode: StatusCodes.Status404NotFound));

app.Run();

public partial class Program;
