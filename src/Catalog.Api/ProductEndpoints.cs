using Catalog.Api.Middleware;
using Catalog.Contracts;
using Catalog.Managers.Products;

namespace Catalog.Api;

internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(
                "/products",
                async (
                    CreateProductRequest request,
                    ProductManager manager,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                {
                    var product = await manager.CreateAsync(
                        new CreateProductCommand(
                            request.Sku,
                            request.Name,
                            request.Description,
                            request.Price),
                        cancellationToken);
                    var response = CreateEnvelope(product, context);

                    return Results.Created($"/products/{product.Id}", response);
                })
            .WithName("CreateProduct")
            .WithTags("Products")
            .Accepts<CreateProductRequest>("application/json")
            .Produces<ProductResponseEnvelope>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints.MapGet(
                "/products/{id}",
                async (
                    string id,
                    ProductManager manager,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                {
                    var product = await manager.GetByIdAsync(id, cancellationToken);
                    return Results.Ok(CreateEnvelope(product, context));
                })
            .WithName("GetProduct")
            .WithTags("Products")
            .Produces<ProductResponseEnvelope>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static ProductResponseEnvelope CreateEnvelope(
        ManagedProduct product,
        HttpContext context) => new(
        new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.Price,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt),
        CorrelationIdMiddleware.GetCorrelationId(context));
}
