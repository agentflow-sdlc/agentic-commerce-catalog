using Catalog.Domain.Categories;

namespace Catalog.Application.Categories;

public interface ICategoryRepository
{
    Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
}
