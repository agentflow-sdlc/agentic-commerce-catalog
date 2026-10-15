namespace Catalog.Application.Products;

/// <summary>Lists every product.</summary>
public sealed class ListProducts(IProductRepository productRepository)
{
    public async Task<IReadOnlyList<ProductView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var products = await productRepository.ListAsync(cancellationToken);
        return products.Select(ProductView.From).ToArray();
    }
}
