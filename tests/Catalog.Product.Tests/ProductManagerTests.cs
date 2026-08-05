using Catalog.Engines.Products;
using Catalog.Managers.Products;
using DomainProduct = Catalog.Engines.Products.Product;

namespace Catalog.Product.Tests;

public sealed class ProductManagerTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 5, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCoordinatesEngineClockIdAndAccessor()
    {
        var accessor = new InMemoryProductAccessor();
        var manager = CreateManager(accessor);

        var product = await manager.CreateAsync(
            new CreateProductCommand(" sku-001 ", " Product ", null, 12.34m),
            CancellationToken.None);

        Assert.Equal("PRODUCT-TEST", product.Id);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal(Timestamp, product.CreatedAt);
        Assert.Equal(product.CreatedAt, product.UpdatedAt);
        Assert.NotNull(await accessor.FindByIdAsync(product.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRejectsDuplicateNormalizedSku()
    {
        var accessor = new InMemoryProductAccessor();
        var manager = CreateManager(accessor);

        await manager.CreateAsync(
            new CreateProductCommand("sku-001", "First", null, 1m),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ProductConflictException>(() =>
            manager.CreateAsync(
                new CreateProductCommand(" SKU-001 ", "Second", null, 2m),
                CancellationToken.None));

        Assert.Equal("SKU-001", exception.Sku);
    }

    [Fact]
    public async Task GetByIdRejectsUnknownProduct()
    {
        var manager = CreateManager(new InMemoryProductAccessor());

        var exception = await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            manager.GetByIdAsync("PRODUCT-UNKNOWN", CancellationToken.None));

        Assert.Equal("PRODUCT-UNKNOWN", exception.ProductId);
    }

    private static ProductManager CreateManager(IProductAccessor accessor) => new(
        accessor,
        new ProductEngine(),
        new FixedProductIdGenerator(),
        new FixedTimeProvider(Timestamp));

    private sealed class FixedProductIdGenerator : IProductIdGenerator
    {
        public string NewId() => "PRODUCT-TEST";
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    private sealed class InMemoryProductAccessor : IProductAccessor
    {
        private readonly Dictionary<string, DomainProduct> _products = new(StringComparer.Ordinal);

        public Task<bool> ExistsBySkuAsync(
            string normalizedSku,
            CancellationToken cancellationToken) => Task.FromResult(
            _products.Values.Any(product => product.Sku == normalizedSku));

        public Task<DomainProduct?> FindByIdAsync(string id, CancellationToken cancellationToken)
        {
            _products.TryGetValue(id, out var product);
            return Task.FromResult(product);
        }

        public Task AddAsync(DomainProduct product, CancellationToken cancellationToken)
        {
            _products.Add(product.Id, product);
            return Task.CompletedTask;
        }
    }
}
