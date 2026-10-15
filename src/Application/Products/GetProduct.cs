using Catalog.Domain.Products;

namespace Catalog.Application.Products;

/// <summary>Reads one product by its id.</summary>
public sealed class GetProduct(IProductRepository productRepository, IProductRules productRules)
{
    public async Task<ProductView> ExecuteAsync(string id, CancellationToken cancellationToken) =>
        ProductView.From(await productRepository.FindRequiredAsync(productRules, id, cancellationToken));
}
