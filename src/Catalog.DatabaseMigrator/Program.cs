using Catalog.Accessors.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["ConnectionStrings:CatalogDb"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Catalog database migration cannot start because ConnectionStrings__CatalogDb is not configured.");
    return 2;
}

builder.Services.AddCatalogAccessors(connectionString);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Catalog.DatabaseMigrator");

try
{
    await using var scope = host.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database;

    MigratorLog.MigrationStarted(logger);
    await database.MigrateAsync();
    MigratorLog.MigrationCompleted(logger);
    return 0;
}
catch (Exception exception)
{
    var errorType = exception.GetType();
    if (logger.IsEnabled(LogLevel.Critical))
    {
        MigratorLog.MigrationFailed(logger, errorType);
    }

    return 1;
}

internal static partial class MigratorLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Catalog database migration started.")]
    public static partial void MigrationStarted(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Catalog database migration completed successfully.")]
    public static partial void MigrationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Critical,
        Message = "Catalog database migration failed with error type {ErrorType}.")]
    public static partial void MigrationFailed(ILogger logger, Type errorType);
}
