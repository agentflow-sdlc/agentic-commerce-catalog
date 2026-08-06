namespace Catalog.Engines.Products;

public interface IProductAccessor
{
    Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken);

    Task<Product?> FindByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task<bool> UpdateStatusAsync(
        string id,
        bool isActive,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}
