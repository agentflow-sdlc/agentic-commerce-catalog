namespace Catalog.Infrastructure.Components;

internal sealed record CatalogNames(
    string ResourceGroup,
    string ContainerRegistry,
    string LogAnalytics,
    string ApplicationInsights,
    string ContainerAppsEnvironment,
    string ManagedIdentity,
    string KeyVault,
    string SqlServer,
    string SqlDatabase,
    string VirtualNetwork,
    string ContainerAppsSubnet,
    string PrivateEndpointsSubnet,
    string SqlPrivateEndpoint,
    string CatalogApi,
    string DatabaseMigratorJob)
{
    public static CatalogNames Create(string suffix, string sqlLocation) => new(
        ResourceGroup: $"rg-agentic-catalog-dev-{suffix}",
        ContainerRegistry: $"agenticcatalog{suffix}",
        LogAnalytics: $"log-agentic-catalog-dev-{suffix}",
        ApplicationInsights: $"appi-agentic-catalog-dev-{suffix}",
        ContainerAppsEnvironment: $"cae-agentic-catalog-dev-{suffix}",
        ManagedIdentity: $"id-agentic-catalog-dev-{suffix}",
        KeyVault: $"kv-agentic-cat-{suffix}",
        SqlServer: $"sql-agentic-catalog-dev-{Normalize(sqlLocation)}-{suffix}",
        SqlDatabase: "catalog",
        VirtualNetwork: $"vnet-agentic-catalog-dev-{suffix}",
        ContainerAppsSubnet: "snet-container-apps",
        PrivateEndpointsSubnet: "snet-private-endpoints",
        SqlPrivateEndpoint: $"pe-sql-agentic-catalog-dev-{suffix}",
        CatalogApi: $"ca-agentic-catalog-api-dev-{suffix}",
        DatabaseMigratorJob: $"caj-agentic-catalog-migrate-dev-{suffix}");

    private static string Normalize(string value) => new(
        value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
}
