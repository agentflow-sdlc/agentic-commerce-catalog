using Catalog.Application.Categories;
using Catalog.Application.Products;
using Catalog.Domain.Categories;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure.Sql;

public static class CatalogSqlServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogSql(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer.EnableRetryOnFailure()));
        services.AddScoped<ICategoryRepository, SqlCategoryRepository>();
        services.AddScoped<IProductRepository, SqlProductRepository>();

        return services;
    }
}
