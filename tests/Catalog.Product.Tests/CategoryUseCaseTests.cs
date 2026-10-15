using Catalog.Application.Categories;
using Catalog.Domain.Categories;

namespace Catalog.Product.Tests;

public sealed class CategoryUseCaseTests
{
    private const string CategoryId = "CATEGORY-00000000-0000-4000-8000-000000000001";
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 6, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCoordinatesRulesTimeIdAndPersistence()
    {
        var repository = new RecordingCategoryRepository();
        var useCases = CreateUseCases(repository);

        var category = await useCases.Create.ExecuteAsync(
            new CreateCategoryCommand("  Home   Appliances ", " Description "),
            CancellationToken.None);

        Assert.Equal(CategoryId, category.Id);
        Assert.Equal("Home Appliances", category.Name);
        Assert.Equal("Description", category.Description);
        Assert.Equal(Timestamp, category.CreatedAt);
        Assert.Equal(1, repository.ExistsByNormalizedNameCallCount);
        Assert.Equal("home appliances", repository.LastNormalizedName);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateRejectsDuplicateNormalizedNameWithoutPersisting()
    {
        var repository = new RecordingCategoryRepository("home appliances");
        var useCases = CreateUseCases(repository);

        var exception = await Assert.ThrowsAsync<CategoryConflictException>(() =>
            useCases.Create.ExecuteAsync(
                new CreateCategoryCommand(" HOME  APPLIANCES "),
                CancellationToken.None));

        Assert.Equal("home appliances", exception.NormalizedName);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateDoesNotCallPersistenceWhenValidationFails()
    {
        var repository = new RecordingCategoryRepository();
        var useCases = CreateUseCases(repository);

        var exception = await Assert.ThrowsAsync<CategoryRequestException>(() =>
            useCases.Create.ExecuteAsync(new CreateCategoryCommand(" "), CancellationToken.None));

        Assert.Equal("CATEGORY_NAME_REQUIRED", exception.Code);
        Assert.Equal(0, repository.ExistsByNormalizedNameCallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task ListReturnsAnEmptyCollection()
    {
        var useCases = CreateUseCases(new RecordingCategoryRepository());

        var categories = await useCases.List.ExecuteAsync(CancellationToken.None);

        Assert.Empty(categories);
    }

    [Fact]
    public async Task ListReturnsCategoriesInRepositoryOrder()
    {
        var repository = new RecordingCategoryRepository();
        repository.Seed(CreateCategory("CATEGORY-00000000-0000-4000-8000-000000000002", "Books", "books"));
        repository.Seed(CreateCategory(CategoryId, "Appliances", "appliances"));
        var useCases = CreateUseCases(repository);

        var categories = await useCases.List.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["Appliances", "Books"], categories.Select(category => category.Name));
        Assert.Equal(1, repository.ListCallCount);
    }

    private sealed record CategoryUseCases(CreateCategory Create, ListCategories List);

    private static CategoryUseCases CreateUseCases(ICategoryRepository repository) => new(
        new CreateCategory(repository, new CategoryRules(), new FixedCategoryIdGenerator(), new FixedTimeProvider()),
        new ListCategories(repository));

    private static Category CreateCategory(string id, string name, string normalizedName) => new(
        id,
        name,
        normalizedName,
        null,
        Timestamp,
        Timestamp);

    private sealed class FixedCategoryIdGenerator : ICategoryIdGenerator
    {
        public string NewId() => CategoryId;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Timestamp;
    }

    private sealed class RecordingCategoryRepository(params string[] duplicateNames)
        : ICategoryRepository
    {
        private readonly HashSet<string> _duplicateNames = new(duplicateNames, StringComparer.Ordinal);
        private readonly List<Category> _categories = [];

        public int ExistsByNormalizedNameCallCount { get; private set; }

        public int AddCallCount { get; private set; }

        public int ListCallCount { get; private set; }

        public string? LastNormalizedName { get; private set; }

        public void Seed(Category category) => _categories.Add(category);

        public Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_categories.Any(category => category.Id == id));

        public Task<bool> ExistsByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken)
        {
            ExistsByNormalizedNameCallCount++;
            LastNormalizedName = normalizedName;
            return Task.FromResult(_duplicateNames.Contains(normalizedName));
        }

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            AddCallCount++;
            _categories.Add(category);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken)
        {
            ListCallCount++;
            IReadOnlyList<Category> result = _categories
                .OrderBy(category => category.NormalizedName, StringComparer.Ordinal)
                .ThenBy(category => category.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }
    }
}
