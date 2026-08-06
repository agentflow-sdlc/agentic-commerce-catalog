using Catalog.Engines.Categories;
using Catalog.Engines.Products;
using Catalog.Managers.Categories;
using Catalog.Managers.Products;
using DomainProduct = Catalog.Engines.Products.Product;

namespace Catalog.Product.Tests;

public sealed class ProductManagerTests
{
    private const string ProductId = "PRODUCT-00000000-0000-4000-8000-000000000001";
    private const string SecondProductId = "PRODUCT-00000000-0000-4000-8000-000000000002";
    private const string UnknownProductId = "PRODUCT-00000000-0000-4000-8000-000000000099";
    private const string CategoryId = "CATEGORY-00000000-0000-4000-8000-000000000001";

    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 6, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCoordinatesEngineClockIdAndPersistenceWithoutCategory()
    {
        var products = new RecordingProductAccessor();
        var categories = new RecordingCategoryAccessor();
        var engine = new RecordingProductEngine();
        var manager = CreateManager(products, categories, engine);

        var product = await manager.CreateAsync(
            new CreateProductCommand(" sku-001 ", " Product ", null, 12.34m),
            CancellationToken.None);

        Assert.Equal(1, engine.CreateCallCount);
        Assert.Equal(ProductId, engine.LastCreatedId);
        Assert.Equal(Timestamp, engine.LastTimestamp);
        Assert.Equal(ProductId, product.Id);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Null(product.CategoryId);
        Assert.Equal(1, products.ExistsBySkuCallCount);
        Assert.Equal(1, products.AddCallCount);
        Assert.Equal(0, categories.ExistsByIdCallCount);
    }

    [Fact]
    public async Task CreateAssociatesAnExistingCategory()
    {
        var products = new RecordingProductAccessor();
        var categories = new RecordingCategoryAccessor(CategoryId);
        var manager = CreateManager(products, categories);

        var product = await manager.CreateAsync(
            new CreateProductCommand("SKU-CATEGORY", "Product", null, 1m, CategoryId),
            CancellationToken.None);

        Assert.Equal(CategoryId, product.CategoryId);
        Assert.Equal(1, categories.ExistsByIdCallCount);
        Assert.Equal(CategoryId, categories.LastRequestedId);
        Assert.Equal(1, products.AddCallCount);
    }

    [Fact]
    public async Task CreateRejectsAnUnknownCategoryWithoutPersisting()
    {
        var products = new RecordingProductAccessor();
        var categories = new RecordingCategoryAccessor();
        var manager = CreateManager(products, categories);

        var exception = await Assert.ThrowsAsync<CategoryNotFoundException>(() =>
            manager.CreateAsync(
                new CreateProductCommand("SKU-CATEGORY", "Product", null, 1m, CategoryId),
                CancellationToken.None));

        Assert.Equal(CategoryId, exception.CategoryId);
        Assert.Equal(1, categories.ExistsByIdCallCount);
        Assert.Equal(0, products.AddCallCount);
    }

    [Fact]
    public async Task CreateRejectsDuplicateNormalizedSkuBeforeCheckingCategoryOrPersisting()
    {
        var products = new RecordingProductAccessor();
        products.Seed(CreateDomainProduct(ProductId, "SKU-001"));
        var categories = new RecordingCategoryAccessor(CategoryId);
        var manager = CreateManager(products, categories);

        var exception = await Assert.ThrowsAsync<ProductConflictException>(() =>
            manager.CreateAsync(
                new CreateProductCommand(" SKU-001 ", "Second", null, 2m, CategoryId),
                CancellationToken.None));

        Assert.Equal("SKU-001", exception.Sku);
        Assert.Equal(1, products.ExistsBySkuCallCount);
        Assert.Equal(0, categories.ExistsByIdCallCount);
        Assert.Equal(0, products.AddCallCount);
    }

    [Fact]
    public async Task CreateDoesNotCallAnyAccessorWhenEngineValidationFails()
    {
        var products = new RecordingProductAccessor();
        var categories = new RecordingCategoryAccessor();
        var manager = CreateManager(products, categories);

        var exception = await Assert.ThrowsAsync<ProductRequestException>(() =>
            manager.CreateAsync(
                new CreateProductCommand(" ", "Invalid", null, 1m, CategoryId),
                CancellationToken.None));

        Assert.Equal("PRODUCT_SKU_REQUIRED", exception.Code);
        Assert.Equal(0, products.ExistsBySkuCallCount);
        Assert.Equal(0, products.AddCallCount);
        Assert.Equal(0, categories.ExistsByIdCallCount);
    }

    [Fact]
    public async Task ListReturnsAnEmptyCollection()
    {
        var manager = CreateManager(new RecordingProductAccessor(), new RecordingCategoryAccessor());

        var products = await manager.ListAsync(CancellationToken.None);

        Assert.Empty(products);
    }

    [Fact]
    public async Task ListPreservesTheDeterministicAccessorOrder()
    {
        var accessor = new RecordingProductAccessor();
        accessor.Seed(CreateDomainProduct(ProductId, "SKU-OLDER", Timestamp.AddMinutes(-1)));
        accessor.Seed(CreateDomainProduct(SecondProductId, "SKU-NEWER", Timestamp));
        var manager = CreateManager(accessor, new RecordingCategoryAccessor());

        var products = await manager.ListAsync(CancellationToken.None);

        Assert.Equal([SecondProductId, ProductId], products.Select(product => product.Id));
        Assert.Equal(1, accessor.ListCallCount);
    }

    [Fact]
    public async Task GetByIdReturnsAnExistingProductThroughTheAccessor()
    {
        var accessor = new RecordingProductAccessor();
        accessor.Seed(CreateDomainProduct(ProductId, "SKU-001"));
        var manager = CreateManager(accessor, new RecordingCategoryAccessor());

        var product = await manager.GetByIdAsync(ProductId, CancellationToken.None);

        Assert.Equal(ProductId, product.Id);
        Assert.Equal(1, accessor.FindByIdCallCount);
        Assert.Equal(ProductId, accessor.LastRequestedId);
    }

    [Fact]
    public async Task GetByIdRejectsUnknownCanonicalProduct()
    {
        var accessor = new RecordingProductAccessor();
        var manager = CreateManager(accessor, new RecordingCategoryAccessor());

        var exception = await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            manager.GetByIdAsync(UnknownProductId, CancellationToken.None));

        Assert.Equal(UnknownProductId, exception.ProductId);
        Assert.Equal(1, accessor.FindByIdCallCount);
    }

    [Fact]
    public async Task GetByIdRejectsInvalidFormatBeforePersistenceAccess()
    {
        var accessor = new RecordingProductAccessor();
        var manager = CreateManager(accessor, new RecordingCategoryAccessor());

        var exception = await Assert.ThrowsAsync<ProductRequestException>(() =>
            manager.GetByIdAsync("PRODUCT-NOT-A-GUID", CancellationToken.None));

        Assert.Equal("PRODUCT_ID_INVALID", exception.Code);
        Assert.Equal(0, accessor.FindByIdCallCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SetStatusDelegatesToTheEngineAndPersistsTheBaselineResult(
        bool initialState,
        bool requestedState)
    {
        var accessor = new RecordingProductAccessor();
        accessor.Seed(CreateDomainProduct(ProductId, "SKU-STATUS") with { IsActive = initialState });
        var engine = new RecordingProductEngine();
        var manager = CreateManager(accessor, new RecordingCategoryAccessor(), engine);

        var product = await manager.SetStatusAsync(
            ProductId,
            requestedState,
            CancellationToken.None);

        Assert.Equal(requestedState, product.IsActive);
        Assert.Equal(Timestamp, product.UpdatedAt);
        Assert.Equal(1, engine.ChangeStatusCallCount);
        Assert.Equal(1, accessor.UpdateStatusCallCount);
        Assert.Equal(requestedState, accessor.LastStatus);
    }

    [Fact]
    public async Task SetStatusRejectsAMissingStateWithoutUpdatingPersistence()
    {
        var accessor = new RecordingProductAccessor();
        accessor.Seed(CreateDomainProduct(ProductId, "SKU-STATUS"));
        var manager = CreateManager(accessor, new RecordingCategoryAccessor());

        var exception = await Assert.ThrowsAsync<ProductRequestException>(() =>
            manager.SetStatusAsync(ProductId, null, CancellationToken.None));

        Assert.Equal("PRODUCT_STATUS_REQUIRED", exception.Code);
        Assert.Equal(0, accessor.UpdateStatusCallCount);
    }

    [Fact]
    public async Task SetStatusRejectsAnUnknownProductWithoutUpdatingPersistence()
    {
        var accessor = new RecordingProductAccessor();
        var manager = CreateManager(accessor, new RecordingCategoryAccessor());

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            manager.SetStatusAsync(UnknownProductId, false, CancellationToken.None));

        Assert.Equal(0, accessor.UpdateStatusCallCount);
    }

    private static ProductManager CreateManager(
        IProductAccessor productAccessor,
        ICategoryAccessor categoryAccessor,
        IProductEngine? engine = null) => new(
        productAccessor,
        categoryAccessor,
        engine ?? new ProductEngine(),
        new FixedProductIdGenerator(),
        new FixedTimeProvider(Timestamp));

    private static DomainProduct CreateDomainProduct(
        string id,
        string sku,
        DateTimeOffset? createdAt = null) => new(
        id,
        sku,
        "Product",
        null,
        1m,
        null,
        true,
        createdAt ?? Timestamp,
        createdAt ?? Timestamp);

    private sealed class FixedProductIdGenerator : IProductIdGenerator
    {
        public string NewId() => ProductId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    private sealed class RecordingProductEngine : IProductEngine
    {
        private readonly ProductEngine _inner = new();

        public int CreateCallCount { get; private set; }

        public int ChangeStatusCallCount { get; private set; }

        public string? LastCreatedId { get; private set; }

        public DateTimeOffset? LastTimestamp { get; private set; }

        public string NormalizeId(string? id) => _inner.NormalizeId(id);

        public DomainProduct Create(
            string id,
            string? sku,
            string? name,
            string? description,
            decimal? price,
            string? categoryId,
            DateTimeOffset timestamp)
        {
            CreateCallCount++;
            LastCreatedId = id;
            LastTimestamp = timestamp;
            return _inner.Create(id, sku, name, description, price, categoryId, timestamp);
        }

        public DomainProduct ChangeStatus(
            DomainProduct product,
            bool? isActive,
            DateTimeOffset timestamp)
        {
            ChangeStatusCallCount++;
            LastTimestamp = timestamp;
            return _inner.ChangeStatus(product, isActive, timestamp);
        }
    }

    private sealed class RecordingProductAccessor : IProductAccessor
    {
        private readonly Dictionary<string, DomainProduct> _products = new(StringComparer.Ordinal);

        public int ExistsBySkuCallCount { get; private set; }

        public int FindByIdCallCount { get; private set; }

        public int ListCallCount { get; private set; }

        public int AddCallCount { get; private set; }

        public int UpdateStatusCallCount { get; private set; }

        public string? LastRequestedId { get; private set; }

        public bool? LastStatus { get; private set; }

        public void Seed(DomainProduct product) => _products.Add(product.Id, product);

        public Task<bool> ExistsBySkuAsync(
            string normalizedSku,
            CancellationToken cancellationToken)
        {
            ExistsBySkuCallCount++;
            return Task.FromResult(_products.Values.Any(product => product.Sku == normalizedSku));
        }

        public Task<DomainProduct?> FindByIdAsync(string id, CancellationToken cancellationToken)
        {
            FindByIdCallCount++;
            LastRequestedId = id;
            _products.TryGetValue(id, out var product);
            return Task.FromResult(product);
        }

        public Task<IReadOnlyList<DomainProduct>> ListAsync(CancellationToken cancellationToken)
        {
            ListCallCount++;
            IReadOnlyList<DomainProduct> result = _products.Values
                .OrderByDescending(product => product.CreatedAt)
                .ThenByDescending(product => product.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task AddAsync(DomainProduct product, CancellationToken cancellationToken)
        {
            AddCallCount++;
            _products.Add(product.Id, product);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateStatusAsync(
            string id,
            bool isActive,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken)
        {
            UpdateStatusCallCount++;
            LastStatus = isActive;
            if (!_products.TryGetValue(id, out var product))
            {
                return Task.FromResult(false);
            }

            _products[id] = product with { IsActive = isActive, UpdatedAt = updatedAt };
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingCategoryAccessor(params string[] existingIds) : ICategoryAccessor
    {
        private readonly HashSet<string> _existingIds = new(existingIds, StringComparer.Ordinal);

        public int ExistsByIdCallCount { get; private set; }

        public string? LastRequestedId { get; private set; }

        public Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken)
        {
            ExistsByIdCallCount++;
            LastRequestedId = id;
            return Task.FromResult(_existingIds.Contains(id));
        }

        public Task<bool> ExistsByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task AddAsync(Category category, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>([]);
    }
}
