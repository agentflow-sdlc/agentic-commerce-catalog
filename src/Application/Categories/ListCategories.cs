namespace Catalog.Application.Categories;

/// <summary>Lists every category.</summary>
public sealed class ListCategories(ICategoryRepository categoryRepository)
{
    public async Task<IReadOnlyList<CategoryView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.ListAsync(cancellationToken);
        return categories.Select(CategoryView.From).ToArray();
    }
}
