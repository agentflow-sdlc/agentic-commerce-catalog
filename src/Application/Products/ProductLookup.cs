using Catalog.Domain.Products;

namespace Catalog.Application.Products;

internal static class ProductLookup
{
    /// <summary>Finds a product by an id the rules normalize first; an unknown id is not found.</summary>
    public static async Task<Product> FindRequiredAsync(
        this IProductRepository productRepository,
        IProductRules productRules,
        string id,
        CancellationToken cancellationToken)
    {
        string normalizedId;
        try
        {
            normalizedId = productRules.NormalizeId(id);
        }
        catch (ProductValidationException exception)
        {
            throw new ProductRequestException(exception.Code, exception.Message, exception);
        }

        return await productRepository.FindByIdAsync(normalizedId, cancellationToken)
            ?? throw new ProductNotFoundException(normalizedId);
    }
}
