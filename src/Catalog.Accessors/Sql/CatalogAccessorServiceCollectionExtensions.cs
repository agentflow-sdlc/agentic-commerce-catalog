using Catalog.Engines.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Accessors.Sql;

public static class CatalogAccessorServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogAccessors(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer.EnableRetryOnFailure()));
        services.AddScoped<IProductAccessor, SqlProductAccessor>();

        return services;
    }
}
