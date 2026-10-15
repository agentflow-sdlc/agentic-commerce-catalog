using Catalog.Application.Categories;
using Catalog.Application.Products;
using Catalog.Domain.Categories;
using Catalog.Domain.Products;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Application;

public static class CatalogApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICategoryRules, CategoryRules>();
        services.AddSingleton<ICategoryIdGenerator, CategoryIdGenerator>();
        services.AddSingleton<IProductRules, ProductRules>();
        services.AddSingleton<IProductIdGenerator, ProductIdGenerator>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateCategory>();
        services.AddScoped<ListCategories>();
        services.AddScoped<CreateProduct>();
        services.AddScoped<ListProducts>();
        services.AddScoped<GetProduct>();
        services.AddScoped<SetProductStatus>();

        return services;
    }
}
