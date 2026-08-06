using Catalog.Engines.Products;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Accessors.Sql;

public sealed class SqlProductAccessor(CatalogDbContext dbContext) : IProductAccessor
{
    public Task<bool> ExistsBySkuAsync(
        string normalizedSku,
        CancellationToken cancellationToken) => dbContext.Products
        .AsNoTracking()
        .AnyAsync(product => product.Sku == normalizedSku, cancellationToken);

    public async Task<Product?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Products
            .AsNoTracking()
            .OrderByDescending(product => product.CreatedAt)
            .ThenByDescending(product => product.Id)
            .Select(product => Map(product))
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        dbContext.Products.Add(Map(product));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ProductSkuAlreadyExistsException(product.Sku, exception);
        }
        catch (DbUpdateException exception)
            when (product.CategoryId is not null
                && exception.InnerException is SqlException { Number: 547 })
        {
            throw new ProductCategoryNotFoundException(product.CategoryId, exception);
        }
    }

    public async Task<bool> UpdateStatusAsync(
        string id,
        bool isActive,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) =>
        await dbContext.Products
            .Where(product => product.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(product => product.IsActive, isActive)
                    .SetProperty(product => product.UpdatedAt, updatedAt),
                cancellationToken) > 0;

    private static Product Map(ProductEntity entity) => new(
        entity.Id,
        entity.Sku,
        entity.Name,
        entity.Description,
        entity.Price,
        entity.CategoryId,
        entity.IsActive,
        entity.CreatedAt,
        entity.UpdatedAt);

    private static ProductEntity Map(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        CategoryId = product.CategoryId,
        IsActive = product.IsActive,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
    };
}
