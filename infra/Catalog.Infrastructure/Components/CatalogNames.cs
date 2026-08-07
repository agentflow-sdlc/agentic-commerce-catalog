using System.Text.RegularExpressions;

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
        // Container Apps caps names at 32 characters, which does not fit the
        // "agentic" and "dev" segments the other resources carry. Both are recoverable
        // from the resource group, so they are dropped here rather than abbreviated into
        // something unreadable, and the shorter names leave room for a longer suffix.
        CatalogApi: ContainerAppsName($"ca-catalog-api-{suffix}"),
        DatabaseMigratorJob: ContainerAppsName($"caj-catalog-migrate-{suffix}"));

    private const int ContainerAppsNameMaxLength = 32;

    private static readonly Regex ContainerAppsNamePattern = new(
        "^[a-z][a-z0-9]*(-[a-z0-9]+)*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Azure enforces the Container Apps naming rules only when the resource is created,
    /// which is after the rest of the stack has already been updated. Validating here
    /// fails during <c>pulumi preview</c> instead, before anything is mutated.
    /// </summary>
    private static string ContainerAppsName(string value)
    {
        if (value.Length is < 2 or > ContainerAppsNameMaxLength)
        {
            throw new InvalidOperationException(
                $"Container Apps name '{value}' is {value.Length} characters; Azure requires " +
                $"between 2 and {ContainerAppsNameMaxLength}.");
        }

        // Lower-case alphanumerics and '-', starting with a letter, ending with an
        // alphanumeric, and never containing '--'.
        if (!ContainerAppsNamePattern.IsMatch(value))
        {
            throw new InvalidOperationException(
                $"Container Apps name '{value}' must be lower-case alphanumerics separated by " +
                "single '-', start with a letter, and end with an alphanumeric.");
        }

        return value;
    }

    private static string Normalize(string value) => new(
        value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
}
