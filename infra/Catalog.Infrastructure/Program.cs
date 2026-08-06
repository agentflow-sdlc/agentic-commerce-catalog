using Catalog.Infrastructure.Components;
using Pulumi;
using Pulumi.AzureNative.Authorization;

return await Deployment.RunAsync(async () =>
{
    var azureConfig = new Config("azure-native");
    var catalogConfig = new Config("catalog");
    var client = await GetClientConfig.InvokeAsync();

    var location = azureConfig.Get("location") ?? "eastus2";
    var suffix = catalogConfig.Require("nameSuffix");
    var deployWorkload = catalogConfig.GetBoolean("deployWorkload") ?? false;
    var sqlAdminLogin = catalogConfig.Get("sqlAdminLogin") ?? "catalogsqladmin";
    var sqlDatabaseSku = catalogConfig.Get("sqlDatabaseSku") ?? "Basic";
    var sqlAdminPassword = catalogConfig.RequireSecret("sqlAdminPassword");
    var names = CatalogNames.Create(suffix);
    var tags = new InputMap<string>
    {
        ["environment"] = "dev",
        ["managed-by"] = "pulumi",
        ["project"] = "agentic-sdlc",
        ["service"] = "catalog",
    };

    var foundation = new CatalogFoundation(
        "catalog-foundation",
        new CatalogFoundationArgs(
            location,
            client.TenantId,
            names,
            sqlAdminLogin,
            sqlAdminPassword,
            sqlDatabaseSku,
            tags));

    CatalogWorkload? workload = null;
    if (deployWorkload)
    {
        workload = new CatalogWorkload(
            "catalog-workload",
            new CatalogWorkloadArgs(
                client.SubscriptionId,
                names,
                foundation,
                catalogConfig.Require("apiImage"),
                catalogConfig.Require("migratorImage"),
                tags));
    }

    return new Dictionary<string, object?>
    {
        ["location"] = location,
        ["resourceGroupName"] = foundation.ResourceGroup.Name,
        ["containerRegistryName"] = foundation.ContainerRegistry.Name,
        ["containerRegistryLoginServer"] = foundation.ContainerRegistryLoginServer,
        ["containerAppsEnvironmentName"] = foundation.ContainerAppsEnvironment.Name,
        ["managedIdentityName"] = foundation.ManagedIdentity.Name,
        ["keyVaultName"] = foundation.KeyVault.Name,
        ["sqlServerName"] = foundation.SqlServer.Name,
        ["sqlDatabaseName"] = foundation.SqlDatabase.Name,
        ["catalogApiName"] = workload?.CatalogApi.Name,
        ["databaseMigratorJobName"] = workload?.DatabaseMigratorJob.Name,
        ["catalogUrl"] = workload?.CatalogUrl,
    };
});
