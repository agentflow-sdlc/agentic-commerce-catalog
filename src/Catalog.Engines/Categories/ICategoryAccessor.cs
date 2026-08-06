namespace Catalog.Engines.Categories;

public interface ICategoryAccessor
{
    Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
}
