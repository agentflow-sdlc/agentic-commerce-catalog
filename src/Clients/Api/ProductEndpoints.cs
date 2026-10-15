using Catalog.Application.Products;
using Catalog.Clients.Api.Contracts;
using Catalog.Clients.Api.Middleware;

namespace Catalog.Clients.Api;

internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(
                "/products",
                async (
                    CreateProductRequest request,
                    CreateProduct createProduct,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductCreateStarted(logger, correlationId);
                    var product = await createProduct.ExecuteAsync(
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
                    ListProducts listProducts,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductListStarted(logger, correlationId);
                    var products = await listProducts.ExecuteAsync(cancellationToken);
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
                    GetProduct getProduct,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductGetStarted(logger, id, correlationId);
                    var product = await getProduct.ExecuteAsync(id, cancellationToken);
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
                    SetProductStatus setProductStatus,
                    HttpContext context,
                    ILogger<ProductEndpointLogCategory> logger,
                    CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
                    CatalogApiLog.ProductStatusChangeStarted(logger, id, correlationId);
                    var product = await setProductStatus.ExecuteAsync(
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
        ProductView product,
        HttpContext context) => new(
        CreateResponse(product),
        CorrelationIdMiddleware.GetCorrelationId(context));

    private static ProductResponse CreateResponse(ProductView product) => new(
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
