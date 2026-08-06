using Catalog.Engines.Categories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Accessors.Sql;

public sealed class SqlCategoryAccessor(CatalogDbContext dbContext) : ICategoryAccessor
{
    public Task<bool> ExistsByIdAsync(
        string id,
        CancellationToken cancellationToken) => dbContext.Categories
        .AsNoTracking()
        .AnyAsync(category => category.Id == id, cancellationToken);

    public Task<bool> ExistsByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken) => dbContext.Categories
        .AsNoTracking()
        .AnyAsync(category => category.NormalizedName == normalizedName, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(category);

        dbContext.Categories.Add(Map(category));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new CategoryNameAlreadyExistsException(category.NormalizedName, exception);
        }
    }

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.NormalizedName)
            .ThenBy(category => category.Id)
            .Select(category => Map(category))
            .ToArrayAsync(cancellationToken);

    private static Category Map(CategoryEntity entity) => new(
        entity.Id,
        entity.Name,
        entity.NormalizedName,
        entity.Description,
        entity.CreatedAt,
        entity.UpdatedAt);

    private static CategoryEntity Map(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        NormalizedName = category.NormalizedName,
        Description = category.Description,
        CreatedAt = category.CreatedAt,
        UpdatedAt = category.UpdatedAt,
    };
}
