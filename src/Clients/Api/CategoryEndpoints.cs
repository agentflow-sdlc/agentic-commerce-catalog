using Catalog.Application.Categories;
using Catalog.Clients.Api.Contracts;
using Catalog.Clients.Api.Middleware;

namespace Catalog.Clients.Api;

internal static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(
                "/categories",
                async (
                    CreateCategoryRequest request,
                    CreateCategory createCategory,
                    HttpContext context,
                    ILogger<CategoryEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.CategoryCreateStarted(logger, correlationId);
                    var category = await createCategory.ExecuteAsync(
                        new CreateCategoryCommand(request.Name, request.Description),
                        cancellationToken);
                    CatalogApiLog.CategoryCreated(logger, category.Id, correlationId);

                    return Results.Json(
                        new CategoryResponseEnvelope(
                            CreateResponse(category),
                            correlationId),
                        statusCode: StatusCodes.Status201Created);
                })
            .WithName("CreateCategory")
            .WithTags("Categories")
            .Accepts<CreateCategoryRequest>("application/json")
            .Produces<CategoryResponseEnvelope>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints.MapGet(
                "/categories",
                async (
                    ListCategories listCategories,
                    HttpContext context,
                    ILogger<CategoryEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.CategoryListStarted(logger, correlationId);
                    var categories = await listCategories.ExecuteAsync(cancellationToken);
                    CatalogApiLog.CategoriesListed(logger, categories.Count, correlationId);

                    return Results.Ok(
                        new CategoryListResponseEnvelope(
                            categories.Select(CreateResponse).ToArray(),
                            correlationId));
                })
            .WithName("ListCategories")
            .WithTags("Categories")
            .Produces<CategoryListResponseEnvelope>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static CategoryResponse CreateResponse(CategoryView category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.CreatedAt,
        category.UpdatedAt);
}

internal sealed class CategoryEndpointLogCategory
{
}
