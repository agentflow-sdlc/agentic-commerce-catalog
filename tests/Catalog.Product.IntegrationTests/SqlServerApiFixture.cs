using Catalog.Accessors.Sql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Catalog.Product.IntegrationTests;

public sealed class SqlServerApiFixture : IAsyncLifetime
{
    // Program reads ConnectionStrings:CatalogDb while building the WebApplicationBuilder,
    // which is earlier than the configuration sources WebApplicationFactory contributes.
    // The environment variable is the documented way to supply it and is read by
    // WebApplication.CreateBuilder itself, so it is in place before Program runs.
    private const string ConnectionStringVariable = "ConnectionStrings__CatalogDb";

    private readonly MsSqlContainer _container = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private WebApplicationFactory<Program>? _factory;
    private string? _previousConnectionString;

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The integration fixture has not started.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _previousConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Environment.SetEnvironmentVariable(
            ConnectionStringVariable,
            _container.GetConnectionString());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("IntegrationTests"));

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var migrations = dbContext.Database.GetMigrations().ToArray();
        if (migrations.Length < 2)
        {
            throw new InvalidOperationException("Product and Category migrations are required.");
        }

        var migrator = dbContext.GetService<IMigrator>();
        var initialProductMigration = migrations.Single(
            migration => migration.EndsWith("_InitialProduct", StringComparison.Ordinal));
        await migrator.MigrateAsync(initialProductMigration);
        await migrator.MigrateAsync();
        Client = _factory.CreateClient();
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM [Products]; DELETE FROM [Categories];");
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
        Environment.SetEnvironmentVariable(ConnectionStringVariable, _previousConnectionString);
    }
}
