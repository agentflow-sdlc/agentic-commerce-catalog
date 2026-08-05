using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Accessors.Sql;

public sealed class DesignTimeCatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    private const string LocalDevelopmentConnection =
        "Server=(localdb)\\mssqllocaldb;Database=AgenticCommerceCatalog;Trusted_Connection=True;TrustServerCertificate=True";

    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CatalogDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = LocalDevelopmentConnection;
        }

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CatalogDbContext(options);
    }
}
