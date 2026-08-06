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
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductCreateStarted(logger, correlationId);
                    var product = await manager.CreateAsync(
                        new CreateProductCommand(
                            request.Sku,
                            request.Name,
                            request.Description,
                            request.Price,
                            request.CategoryId),
                        cancellationToken);
                    var response = CreateEnvelope(product, context);
                    CatalogApiLog.ProductCreated(logger, product.Id, correlationId);
                    if (product.CategoryId is not null)
                    {
                        CatalogApiLog.ProductCategoryAssociated(
                            logger,
                            product.Id,
                            product.CategoryId,
                            correlationId);
                    }

                    return Results.Created($"/products/{product.Id}", response);
                })
            .WithName("CreateProduct")
            .WithTags("Products")
            .Accepts<CreateProductRequest>("application/json")
            .Produces<ProductResponseEnvelope>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints.MapGet(
                "/products",
                async (
                    ProductManager manager,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductListStarted(logger, correlationId);
                    var products = await manager.ListAsync(cancellationToken);
                    CatalogApiLog.ProductsListed(logger, products.Count, correlationId);
                    return Results.Ok(
                        new ProductListResponseEnvelope(
                            products.Select(CreateResponse).ToArray(),
                            correlationId));
                })
            .WithName("ListProducts")
            .WithTags("Products")
            .Produces<ProductListResponseEnvelope>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints.MapGet(
                "/products/{id}",
                async (
                    string id,
                    ProductManager manager,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductGetStarted(logger, id, correlationId);
                    var product = await manager.GetByIdAsync(id, cancellationToken);
                    CatalogApiLog.ProductRetrieved(logger, product.Id, correlationId);
                    return Results.Ok(CreateEnvelope(product, context));
                })
            .WithName("GetProduct")
            .WithTags("Products")
            .Produces<ProductResponseEnvelope>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints.MapPatch(
                "/products/{id}/status",
                async (
                    string id,
                    UpdateProductStatusRequest request,
                    ProductManager manager,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductStatusChangeStarted(logger, id, correlationId);
                    var product = await manager.SetStatusAsync(
                        id,
                        request.IsActive,
                        cancellationToken);
                    CatalogApiLog.ProductStatusChanged(
                        logger,
                        product.Id,
                        product.IsActive,
                        correlationId);
                    return Results.Ok(CreateEnvelope(product, context));
                })
            .WithName("UpdateProductStatus")
            .WithTags("Products")
            .Accepts<UpdateProductStatusRequest>("application/json")
            .Produces<ProductResponseEnvelope>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static ProductResponseEnvelope CreateEnvelope(
        ManagedProduct product,
        HttpContext context) => new(
        CreateResponse(product),
        CorrelationIdMiddleware.GetCorrelationId(context));

    private static ProductResponse CreateResponse(ManagedProduct product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Price,
        product.CategoryId,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}

internal sealed class ProductEndpointLogCategory
{
}
