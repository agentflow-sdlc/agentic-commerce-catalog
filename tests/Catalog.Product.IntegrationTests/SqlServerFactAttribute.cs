namespace Catalog.Product.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_SQL_SERVER_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set RUN_SQL_SERVER_TESTS=true on a Docker-enabled host to run SQL Server integration tests.";
        }
    }
}
