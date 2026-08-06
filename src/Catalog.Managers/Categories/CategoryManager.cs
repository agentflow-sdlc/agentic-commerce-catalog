using Catalog.Engines.Categories;

namespace Catalog.Managers.Categories;

public sealed class CategoryManager(
    ICategoryAccessor categoryAccessor,
    ICategoryEngine categoryEngine,
    ICategoryIdGenerator idGenerator,
    TimeProvider timeProvider)
{
    public async Task<ManagedCategory> CreateAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Category category;
        try
        {
            category = categoryEngine.Create(
                idGenerator.NewId(),
                command.Name,
                command.Description,
                timeProvider.GetUtcNow());
        }
        catch (CategoryValidationException exception)
        {
            throw new CategoryRequestException(exception.Code, exception.Message, exception);
        }

        if (await categoryAccessor.ExistsByNormalizedNameAsync(
                category.NormalizedName,
                cancellationToken))
        {
            throw new CategoryConflictException(category.NormalizedName);
        }

        try
        {
            await categoryAccessor.AddAsync(category, cancellationToken);
        }
        catch (CategoryNameAlreadyExistsException exception)
        {
            throw new CategoryConflictException(category.NormalizedName, exception);
        }

        return Map(category);
    }

    public async Task<IReadOnlyList<ManagedCategory>> ListAsync(
        CancellationToken cancellationToken)
    {
        var categories = await categoryAccessor.ListAsync(cancellationToken);
        return categories.Select(Map).ToArray();
    }

    private static ManagedCategory Map(Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.CreatedAt,
        category.UpdatedAt);
}
