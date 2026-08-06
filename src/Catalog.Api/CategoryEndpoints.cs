using Catalog.Api.Middleware;
using Catalog.Contracts;
using Catalog.Managers.Categories;

namespace Catalog.Api;

internal static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(
                "/categories",
                async (
                    CreateCategoryRequest request,
                    CategoryManager manager,
                    HttpContext context,
                    ILogger<CategoryEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.CategoryCreateStarted(logger, correlationId);
                    var category = await manager.CreateAsync(
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
                    CategoryManager manager,
                    HttpContext context,
                    ILogger<CategoryEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.CategoryListStarted(logger, correlationId);
                    var categories = await manager.ListAsync(cancellationToken);
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

    private static CategoryResponse CreateResponse(ManagedCategory category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.CreatedAt,
        category.UpdatedAt);
}

internal sealed class CategoryEndpointLogCategory
{
}
