using Catalog.Engines.Products;
using Catalog.Managers.Products;
using DomainProduct = Catalog.Engines.Products.Product;

namespace Catalog.Product.Tests;

public sealed class ProductManagerTests
{
    private const string ProductId = "PRODUCT-00000000-0000-4000-8000-000000000001";
    private const string UnknownProductId = "PRODUCT-00000000-0000-4000-8000-000000000099";

    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 5, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCoordinatesEngineClockIdAndPersistence()
    {
        var accessor = new RecordingProductAccessor();
        var engine = new RecordingProductEngine();
        var manager = CreateManager(accessor, engine);

        var product = await manager.CreateAsync(
            new CreateProductCommand(" sku-001 ", " Product ", null, 12.34m),
            CancellationToken.None);

        Assert.Equal(1, engine.CreateCallCount);
        Assert.Equal(ProductId, engine.LastCreatedId);
        Assert.Equal(Timestamp, engine.LastTimestamp);
        Assert.Equal(ProductId, product.Id);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal(Timestamp, product.CreatedAt);
        Assert.Equal(product.CreatedAt, product.UpdatedAt);
        Assert.Equal(1, accessor.ExistsBySkuCallCount);
        Assert.Equal(1, accessor.AddCallCount);
        Assert.Equal(product.Id, accessor.LastAddedProduct?.Id);
    }

    [Fact]
    public async Task CreateRejectsDuplicateNormalizedSkuWithoutPersisting()
    {
        var accessor = new RecordingProductAccessor();
        accessor.Seed(CreateDomainProduct(ProductId, "SKU-001"));
        var manager = CreateManager(accessor);

        var exception = await Assert.ThrowsAsync<ProductConflictException>(() =>
            manager.CreateAsync(
                new CreateProductCommand(" SKU-001 ", "Second", null, 2m),
                CancellationToken.None));

        Assert.Equal("SKU-001", exception.Sku);
        Assert.Equal(1, accessor.ExistsBySkuCallCount);
        Assert.Equal(0, accessor.AddCallCount);
    }

    [Fact]
    public async Task CreateDoesNotCallPersistenceWhenEngineValidationFails()
    {
        var accessor = new RecordingProductAccessor();
        var manager = CreateManager(accessor);

        var exception = await Assert.ThrowsAsync<ProductRequestException>(() =>
            manager.CreateAsync(
                new CreateProductCommand(" ", "Invalid", null, 1m),
                CancellationToken.None));

        Assert.Equal("PRODUCT_SKU_REQUIRED", exception.Code);
        Assert.Equal(0, accessor.ExistsBySkuCallCount);
        Assert.Equal(0, accessor.AddCallCount);
    }

    [Fact]
    public async Task GetByIdReturnsAnExistingProductThroughTheAccessor()
    {
        var accessor = new RecordingProductAccessor();
        accessor.Seed(CreateDomainProduct(ProductId, "SKU-001"));
        var manager = CreateManager(accessor);

        var product = await manager.GetByIdAsync(ProductId, CancellationToken.None);

        Assert.Equal(ProductId, product.Id);
        Assert.Equal(1, accessor.FindByIdCallCount);
        Assert.Equal(ProductId, accessor.LastRequestedId);
    }

    [Fact]
    public async Task GetByIdRejectsUnknownCanonicalProduct()
    {
        var accessor = new RecordingProductAccessor();
        var manager = CreateManager(accessor);

        var exception = await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            manager.GetByIdAsync(UnknownProductId, CancellationToken.None));

        Assert.Equal(UnknownProductId, exception.ProductId);
        Assert.Equal(1, accessor.FindByIdCallCount);
    }

    [Fact]
    public async Task GetByIdRejectsInvalidFormatBeforePersistenceAccess()
    {
        var accessor = new RecordingProductAccessor();
        var manager = CreateManager(accessor);

        var exception = await Assert.ThrowsAsync<ProductRequestException>(() =>
            manager.GetByIdAsync("PRODUCT-NOT-A-GUID", CancellationToken.None));

        Assert.Equal("PRODUCT_ID_INVALID", exception.Code);
        Assert.Equal(0, accessor.FindByIdCallCount);
    }

    private static ProductManager CreateManager(
        IProductAccessor accessor,
        IProductEngine? engine = null) => new(
        accessor,
        engine ?? new ProductEngine(),
        new FixedProductIdGenerator(),
        new FixedTimeProvider(Timestamp));

    private static DomainProduct CreateDomainProduct(string id, string sku) => new(
        id,
        sku,
        "Product",
        null,
        1m,
        true,
        Timestamp,
        Timestamp);

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

        public string? LastCreatedId { get; private set; }

        public DateTimeOffset? LastTimestamp { get; private set; }

        public string NormalizeId(string? id) => _inner.NormalizeId(id);

        public DomainProduct Create(
            string id,
            string? sku,
            string? name,
            string? description,
            decimal? price,
            DateTimeOffset timestamp)
        {
            CreateCallCount++;
            LastCreatedId = id;
            LastTimestamp = timestamp;
            return _inner.Create(id, sku, name, description, price, timestamp);
        }
    }

    private sealed class RecordingProductAccessor : IProductAccessor
    {
        private readonly Dictionary<string, DomainProduct> _products = new(StringComparer.Ordinal);

        public int ExistsBySkuCallCount { get; private set; }

        public int FindByIdCallCount { get; private set; }

        public int AddCallCount { get; private set; }

        public string? LastRequestedId { get; private set; }

        public DomainProduct? LastAddedProduct { get; private set; }

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

        public Task AddAsync(DomainProduct product, CancellationToken cancellationToken)
        {
            AddCallCount++;
            LastAddedProduct = product;
            _products.Add(product.Id, product);
            return Task.CompletedTask;
        }
    }
}
