using Catalog.Engines.Products;
using Catalog.Managers.Products;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Managers;

public static class CatalogManagerServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogManagers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProductEngine, ProductEngine>();
        services.AddSingleton<IProductIdGenerator, ProductIdGenerator>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProductManager>();

        return services;
    }
}
