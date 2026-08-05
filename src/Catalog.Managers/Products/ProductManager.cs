using Catalog.Engines.Products;

namespace Catalog.Managers.Products;

public sealed class ProductManager
{
    private readonly IProductAccessor _productAccessor;
    private readonly IProductEngine _productEngine;
    private readonly IProductIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ProductManager(
        IProductAccessor productAccessor,
        IProductEngine productEngine,
        IProductIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        _productAccessor = productAccessor;
        _productEngine = productEngine;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<ManagedProduct> CreateAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Product product;
        try
        {
            product = _productEngine.Create(
                _idGenerator.NewId(),
                command.Sku,
                command.Name,
                command.Description,
                command.Price,
                _timeProvider.GetUtcNow());
        }
        catch (ProductValidationException exception)
        {
            throw new ProductRequestException(exception.Code, exception.Message, exception);
        }

        if (await _productAccessor.ExistsBySkuAsync(product.Sku, cancellationToken))
        {
            throw new ProductConflictException(product.Sku);
        }

        try
        {
            await _productAccessor.AddAsync(product, cancellationToken);
        }
        catch (ProductSkuAlreadyExistsException exception)
        {
            throw new ProductConflictException(product.Sku, exception);
        }

        return Map(product);
    }

    public async Task<ManagedProduct> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var product = await _productAccessor.FindByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        return Map(product);
    }

    private static ManagedProduct Map(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Price,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}
