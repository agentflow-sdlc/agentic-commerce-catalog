using Microsoft.EntityFrameworkCore;

namespace Catalog.Accessors.Sql;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options)
{
    internal DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
