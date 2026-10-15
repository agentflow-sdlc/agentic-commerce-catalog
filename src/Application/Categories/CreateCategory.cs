using Catalog.Domain.Categories;

namespace Catalog.Application.Categories;

/// <summary>Creates a category whose normalized name is unique.</summary>
public sealed class CreateCategory(
    ICategoryRepository categoryRepository,
    ICategoryRules categoryRules,
    ICategoryIdGenerator idGenerator,
    TimeProvider timeProvider)
{
    public async Task<CategoryView> ExecuteAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Category category;
        try
        {
            category = categoryRules.Create(
                idGenerator.NewId(),
                command.Name,
                command.Description,
                timeProvider.GetUtcNow());
        }
        catch (CategoryValidationException exception)
        {
            throw new CategoryRequestException(exception.Code, exception.Message, exception);
        }

        if (await categoryRepository.ExistsByNormalizedNameAsync(
                category.NormalizedName,
                cancellationToken))
        {
            throw new CategoryConflictException(category.NormalizedName);
        }

        try
        {
            await categoryRepository.AddAsync(category, cancellationToken);
        }
        catch (CategoryNameAlreadyExistsException exception)
        {
            throw new CategoryConflictException(category.NormalizedName, exception);
        }

        return CategoryView.From(category);
    }
}
