using Catalog.Infrastructure.Components;
using Pulumi;
using Pulumi.AzureNative.Authorization;

return await Deployment.RunAsync(async () =>
{
    var azureConfig = new Config("azure-native");
    var catalogConfig = new Config("catalog");
    var client = await GetClientConfig.InvokeAsync();

    var location = azureConfig.Get("location") ?? "eastus2";
    var sqlLocation = catalogConfig.Get("sqlLocation") ?? location;
    var suffix = catalogConfig.Require("nameSuffix");
    var deployWorkload = catalogConfig.GetBoolean("deployWorkload") ?? false;
    var sqlAdminLogin = catalogConfig.Get("sqlAdminLogin") ?? "catalogsqladmin";
    var sqlDatabaseSku = catalogConfig.Get("sqlDatabaseSku") ?? "Basic";
    var sqlAdminPassword = catalogConfig.RequireSecret("sqlAdminPassword");

    // Cost guardrails. `poc-free` selects the free or minimum option everywhere a choice
    // exists; anything outside the free path additionally requires allowPaidResources.
    // Both defaults are the cheap ones on purpose, so an expensive resource cannot appear
    // by accident - it takes a deliberate configuration change.
    var costProfile = catalogConfig.Get("costProfile") ?? "poc-free";
    var allowPaidResources = catalogConfig.GetBoolean("allowPaidResources") ?? false;
    var freeFirst = costProfile == "poc-free" && !allowPaidResources;

    if (costProfile != "poc-free" && !allowPaidResources)
    {
        throw new InvalidOperationException(
            $"costProfile '{costProfile}' leaves the free-first path, so it requires " +
            "catalog:allowPaidResources=true to be set explicitly.");
    }

    // Opt-in and deliberately NOT implied by the free-first profile: the Azure SQL free
    // offer can only be set when a database is created, so enabling it replaces the
    // existing database and destroys its data.
    // A private endpoint is the only way to keep Azure SQL off the public network here,
    // and it costs roughly $7/month, so it stays an explicit opt-in rather than part of
    // the free path. Without it the server needs a firewall rule open to all of Azure.
    var sqlPrivateEndpoint = catalogConfig.GetBoolean("sqlPrivateEndpoint") ?? false;
    var sqlUseFreeOffer = catalogConfig.GetBoolean("sqlUseFreeOffer") ?? false;

    // Cost alerting. Without a contact address no budget is created, because a budget
    // nobody is notified about does not protect anything.
    var budgetContactEmail = catalogConfig.Get("budgetContactEmail");
    var budgetAmountUsd = catalogConfig.GetDouble("budgetAmountUsd") ?? 10;
    var budgetStartDate = catalogConfig.Get("budgetStartDate") ?? "2026-08-01T00:00:00Z";

    var names = CatalogNames.Create(suffix, sqlLocation);
    var tags = new InputMap<string>
    {
        ["environment"] = "dev",
        ["managed-by"] = "pulumi",
        ["project"] = "agentic-sdlc",
        ["service"] = "catalog",
        ["purpose"] = "agentic-sdlc-poc",
        ["cost-profile"] = freeFirst ? "free-first" : costProfile,
    };

    var foundation = new CatalogFoundation(
        "catalog-foundation",
        new CatalogFoundationArgs(
            location,
            sqlLocation,
            client.TenantId,
            names,
            sqlAdminLogin,
            sqlAdminPassword,
            sqlDatabaseSku,
            freeFirst,
            sqlPrivateEndpoint,
            sqlUseFreeOffer,
            budgetContactEmail,
            budgetAmountUsd,
            budgetStartDate,
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
        ["sqlLocation"] = sqlLocation,
        ["costProfile"] = costProfile,
        ["allowPaidResources"] = allowPaidResources,
        ["sqlPrivateEndpoint"] = sqlPrivateEndpoint,
        ["resourceGroupName"] = foundation.ResourceGroup.Name,
        // Null under the free-first profile: images come from GitHub Container Registry,
        // so no Azure Container Registry is provisioned and nothing pays for one.
        ["containerRegistryName"] = foundation.ContainerRegistry?.Name,
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
