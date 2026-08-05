namespace Catalog.Engines.Products;

public interface IProductAccessor
{
    Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken);

    Task<Product?> FindByIdAsync(string id, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);
}
