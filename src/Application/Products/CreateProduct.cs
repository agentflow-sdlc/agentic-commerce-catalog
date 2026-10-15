using Catalog.Application.Categories;
using Catalog.Domain.Products;

namespace Catalog.Application.Products;

/// <summary>Creates a product with a unique SKU, optionally in an existing category.</summary>
public sealed class CreateProduct(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IProductRules productRules,
    IProductIdGenerator idGenerator,
    TimeProvider timeProvider)
{
    public async Task<ProductView> ExecuteAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Product product;
        try
        {
            product = productRules.Create(
                idGenerator.NewId(),
                command.Sku,
                command.Name,
                command.Description,
                command.Price,
                command.CategoryId,
                timeProvider.GetUtcNow());
        }
        catch (ProductValidationException exception)
        {
            throw new ProductRequestException(exception.Code, exception.Message, exception);
        }

        if (await productRepository.ExistsBySkuAsync(product.Sku, cancellationToken))
        {
            throw new ProductConflictException(product.Sku);
        }

        if (product.CategoryId is not null
            && !await categoryRepository.ExistsByIdAsync(product.CategoryId, cancellationToken))
        {
            throw new CategoryNotFoundException(product.CategoryId);
        }

        try
        {
            await productRepository.AddAsync(product, cancellationToken);
        }
        catch (ProductSkuAlreadyExistsException exception)
        {
            throw new ProductConflictException(product.Sku, exception);
        }
        catch (ProductCategoryNotFoundException exception)
        {
            throw new CategoryNotFoundException(exception.CategoryId, exception);
        }

        return ProductView.From(product);
    }
}
