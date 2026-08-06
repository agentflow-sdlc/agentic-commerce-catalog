using Catalog.Engines.Categories;
using Catalog.Engines.Products;
using Catalog.Managers.Categories;

namespace Catalog.Managers.Products;

public sealed class ProductManager
{
    private readonly IProductAccessor _productAccessor;
    private readonly ICategoryAccessor _categoryAccessor;
    private readonly IProductEngine _productEngine;
    private readonly IProductIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ProductManager(
        IProductAccessor productAccessor,
        ICategoryAccessor categoryAccessor,
        IProductEngine productEngine,
        IProductIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        _productAccessor = productAccessor;
        _categoryAccessor = categoryAccessor;
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
                command.CategoryId,
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

        if (product.CategoryId is not null
            && !await _categoryAccessor.ExistsByIdAsync(product.CategoryId, cancellationToken))
        {
            throw new CategoryNotFoundException(product.CategoryId);
        }

        try
        {
            await _productAccessor.AddAsync(product, cancellationToken);
        }
        catch (ProductSkuAlreadyExistsException exception)
        {
            throw new ProductConflictException(product.Sku, exception);
        }
        catch (ProductCategoryNotFoundException exception)
        {
            throw new CategoryNotFoundException(exception.CategoryId, exception);
        }

        return Map(product);
    }

    public async Task<IReadOnlyList<ManagedProduct>> ListAsync(
        CancellationToken cancellationToken)
    {
        var products = await _productAccessor.ListAsync(cancellationToken);
        return products.Select(Map).ToArray();
    }

    public async Task<ManagedProduct> SetStatusAsync(
        string id,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var existing = await FindRequiredAsync(id, cancellationToken);

        Product updated;
        try
        {
            updated = _productEngine.ChangeStatus(
                existing,
                isActive,
                _timeProvider.GetUtcNow());
        }
        catch (ProductValidationException exception)
        {
            throw new ProductRequestException(exception.Code, exception.Message, exception);
        }

        if (!await _productAccessor.UpdateStatusAsync(
                updated.Id,
                updated.IsActive,
                updated.UpdatedAt,
                cancellationToken))
        {
            throw new ProductNotFoundException(updated.Id);
        }

        return Map(updated);
    }

    public async Task<ManagedProduct> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        return Map(await FindRequiredAsync(id, cancellationToken));
    }

    private async Task<Product> FindRequiredAsync(
        string id,
        CancellationToken cancellationToken)
    {
        string normalizedId;
        try
        {
            normalizedId = _productEngine.NormalizeId(id);
        }
        catch (ProductValidationException exception)
        {
            throw new ProductRequestException(exception.Code, exception.Message, exception);
        }

        return await _productAccessor.FindByIdAsync(normalizedId, cancellationToken)
            ?? throw new ProductNotFoundException(normalizedId);
    }

    private static ManagedProduct Map(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Price,
        product.CategoryId,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}
