using Catalog.Domain.Products;

namespace Catalog.Application.Products;

/// <summary>Activates or deactivates a product.</summary>
public sealed class SetProductStatus(
    IProductRepository productRepository,
    IProductRules productRules,
    TimeProvider timeProvider)
{
    public async Task<ProductView> ExecuteAsync(
        string id,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var existing = await productRepository.FindRequiredAsync(productRules, id, cancellationToken);

        Product updated;
        try
        {
            updated = productRules.ChangeStatus(existing, isActive, timeProvider.GetUtcNow());
        }
        catch (ProductValidationException exception)
        {
            throw new ProductRequestException(exception.Code, exception.Message, exception);
        }

        if (!await productRepository.UpdateStatusAsync(
                updated.Id,
                updated.IsActive,
                updated.UpdatedAt,
                cancellationToken))
        {
            throw new ProductNotFoundException(updated.Id);
        }

        return ProductView.From(updated);
    }
}
