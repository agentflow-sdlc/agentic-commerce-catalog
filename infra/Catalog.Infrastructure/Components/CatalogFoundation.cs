using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.ApplicationInsights;
using Pulumi.AzureNative.ContainerRegistry;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.ManagedIdentity;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.PrivateDns;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Sql;
using AppInputs = Pulumi.AzureNative.App.Inputs;
using ApplicationInsightsInputs = Pulumi.AzureNative.ApplicationInsights.Inputs;
using ContainerRegistryInputs = Pulumi.AzureNative.ContainerRegistry.Inputs;
using KeyVaultInputs = Pulumi.AzureNative.KeyVault.Inputs;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using OperationalInsightsInputs = Pulumi.AzureNative.OperationalInsights.Inputs;
using PrivateDnsInputs = Pulumi.AzureNative.PrivateDns.Inputs;
using SqlInputs = Pulumi.AzureNative.Sql.Inputs;

namespace Catalog.Infrastructure.Components;

internal sealed class CatalogFoundation : ComponentResource
{
    public CatalogFoundation(
        string name,
        CatalogFoundationArgs args,
        ComponentResourceOptions? options = null)
        : base("agentflow:catalog:CatalogFoundation", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        ResourceGroup = new ResourceGroup(
            "catalog-resource-group",
            new ResourceGroupArgs
            {
                ResourceGroupName = args.Names.ResourceGroup,
                Location = args.Location,
                Tags = args.Tags,
            },
            childOptions);

        VirtualNetwork = new VirtualNetwork(
            "catalog-vnet",
            new VirtualNetworkArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                VirtualNetworkName = args.Names.VirtualNetwork,
                Location = ResourceGroup.Location,
                AddressSpace = new NetworkInputs.AddressSpaceArgs
                {
                    AddressPrefixes = ["10.42.0.0/16"],
                },
                Tags = args.Tags,
            },
            childOptions);

        ContainerAppsSubnet = new Subnet(
            "container-apps-subnet",
            new SubnetArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                VirtualNetworkName = VirtualNetwork.Name,
                SubnetName = args.Names.ContainerAppsSubnet,
                AddressPrefix = "10.42.0.0/23",
                Delegations =
                [
                    new NetworkInputs.DelegationArgs
                    {
                        Name = "container-apps-environments",
                        ServiceName = "Microsoft.App/environments",
                    },
                ],
            },
            childOptions);

        PrivateEndpointsSubnet = new Subnet(
            "private-endpoints-subnet",
            new SubnetArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                VirtualNetworkName = VirtualNetwork.Name,
                SubnetName = args.Names.PrivateEndpointsSubnet,
                AddressPrefix = "10.42.2.0/27",
                PrivateEndpointNetworkPolicies = "Disabled",
            },
            childOptions);

        ContainerRegistry = new Registry(
            "catalog-registry",
            new RegistryArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                RegistryName = args.Names.ContainerRegistry,
                Location = ResourceGroup.Location,
                AdminUserEnabled = false,
                AnonymousPullEnabled = false,
                PublicNetworkAccess = "Enabled",
                NetworkRuleBypassOptions = "AzureServices",
                Sku = new ContainerRegistryInputs.SkuArgs
                {
                    Name = "Basic",
                },
                Tags = args.Tags,
            },
            childOptions);

        LogAnalytics = new Workspace(
            "catalog-log-analytics",
            new WorkspaceArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                WorkspaceName = args.Names.LogAnalytics,
                Location = ResourceGroup.Location,
                RetentionInDays = 30,
                PublicNetworkAccessForIngestion = "Enabled",
                PublicNetworkAccessForQuery = "Enabled",
                Sku = new OperationalInsightsInputs.WorkspaceSkuArgs
                {
                    Name = "PerGB2018",
                },
                Tags = args.Tags,
            },
            childOptions);

        ApplicationInsights = new Component(
            "catalog-application-insights",
            new ComponentArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                ResourceName = args.Names.ApplicationInsights,
                Location = ResourceGroup.Location,
                ApplicationType = "web",
                Kind = "web",
                DisableIpMasking = false,
                DisableLocalAuth = true,
                IngestionMode = "LogAnalytics",
                PublicNetworkAccessForIngestion = "Enabled",
                PublicNetworkAccessForQuery = "Enabled",
                RetentionInDays = 30,
                SamplingPercentage = 100,
                WorkspaceResourceId = LogAnalytics.Id,
                Tags = args.Tags,
            },
            childOptions);

        ManagedIdentity = new UserAssignedIdentity(
            "catalog-managed-identity",
            new UserAssignedIdentityArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                ResourceName = args.Names.ManagedIdentity,
                Location = ResourceGroup.Location,
                Tags = args.Tags,
            },
            childOptions);

        KeyVault = new Vault(
            "catalog-key-vault",
            new VaultArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                VaultName = args.Names.KeyVault,
                Location = ResourceGroup.Location,
                Properties = new KeyVaultInputs.VaultPropertiesArgs
                {
                    TenantId = args.TenantId,
                    EnableRbacAuthorization = true,
                    EnableSoftDelete = true,
                    EnablePurgeProtection = true,
                    SoftDeleteRetentionInDays = 7,
                    PublicNetworkAccess = "Enabled",
                    NetworkAcls = new KeyVaultInputs.NetworkRuleSetArgs
                    {
                        Bypass = "AzureServices",
                        DefaultAction = "Allow",
                    },
                    Sku = new KeyVaultInputs.SkuArgs
                    {
                        Family = "A",
                        Name = Pulumi.AzureNative.KeyVault.SkuName.Standard,
                    },
                },
                Tags = args.Tags,
            },
            childOptions);

        SqlServer = new Server(
            "catalog-sql-server",
            new ServerArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                ServerName = args.Names.SqlServer,
                Location = ResourceGroup.Location,
                AdministratorLogin = args.SqlAdminLogin,
                AdministratorLoginPassword = args.SqlAdminPassword,
                MinimalTlsVersion = "1.2",
                PublicNetworkAccess = "Disabled",
                RestrictOutboundNetworkAccess = "Enabled",
                Version = "12.0",
                Tags = args.Tags,
            },
            childOptions);

        SqlDatabase = new Database(
            "catalog-sql-database",
            new DatabaseArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                ServerName = SqlServer.Name,
                DatabaseName = args.Names.SqlDatabase,
                Location = ResourceGroup.Location,
                MaxSizeBytes = 2_147_483_648,
                RequestedBackupStorageRedundancy = "Local",
                Sku = new SqlInputs.SkuArgs
                {
                    Name = args.SqlDatabaseSku,
                    Tier = args.SqlDatabaseSku,
                },
                Tags = args.Tags,
            },
            childOptions);

        SqlPrivateDnsZone = new PrivateZone(
            "catalog-sql-private-dns-zone",
            new PrivateZoneArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                PrivateZoneName = "privatelink.database.windows.net",
                Location = "global",
                Tags = args.Tags,
            },
            childOptions);

        _ = new VirtualNetworkLink(
            "catalog-sql-private-dns-vnet-link",
            new VirtualNetworkLinkArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                PrivateZoneName = SqlPrivateDnsZone.Name,
                VirtualNetworkLinkName = "catalog-vnet-link",
                Location = "global",
                RegistrationEnabled = false,
                VirtualNetwork = new PrivateDnsInputs.SubResourceArgs
                {
                    Id = VirtualNetwork.Id,
                },
                Tags = args.Tags,
            },
            childOptions);

        SqlPrivateEndpoint = new PrivateEndpoint(
            "catalog-sql-private-endpoint",
            new PrivateEndpointArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                PrivateEndpointName = args.Names.SqlPrivateEndpoint,
                Location = ResourceGroup.Location,
                Subnet = new NetworkInputs.SubnetArgs
                {
                    Id = PrivateEndpointsSubnet.Id,
                },
                PrivateLinkServiceConnections =
                [
                    new NetworkInputs.PrivateLinkServiceConnectionArgs
                    {
                        Name = "catalog-sql",
                        PrivateLinkServiceId = SqlServer.Id,
                        GroupIds = ["sqlServer"],
                        PrivateLinkServiceConnectionState =
                            new NetworkInputs.PrivateLinkServiceConnectionStateArgs
                            {
                                Status = "Approved",
                                Description = "Catalog Container Apps private SQL access.",
                            },
                    },
                ],
                Tags = args.Tags,
            },
            childOptions);

        _ = new PrivateDnsZoneGroup(
            "catalog-sql-private-dns-zone-group",
            new PrivateDnsZoneGroupArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                PrivateEndpointName = SqlPrivateEndpoint.Name,
                PrivateDnsZoneGroupName = "default",
                PrivateDnsZoneConfigs =
                [
                    new NetworkInputs.PrivateDnsZoneConfigArgs
                    {
                        Name = "sql",
                        PrivateDnsZoneId = SqlPrivateDnsZone.Id,
                    },
                ],
            },
            childOptions);

        var workspaceKeys = GetSharedKeys.Invoke(
            new GetSharedKeysInvokeArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                WorkspaceName = LogAnalytics.Name,
            });

        ContainerAppsEnvironment = new ManagedEnvironment(
            "catalog-container-apps-environment",
            new ManagedEnvironmentArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                EnvironmentName = args.Names.ContainerAppsEnvironment,
                Location = ResourceGroup.Location,
                PublicNetworkAccess = "Enabled",
                AppLogsConfiguration = new AppInputs.AppLogsConfigurationArgs
                {
                    Destination = "log-analytics",
                    LogAnalyticsConfiguration = new AppInputs.LogAnalyticsConfigurationArgs
                    {
                        CustomerId = LogAnalytics.CustomerId,
                        SharedKey = Output.CreateSecret(
                            workspaceKeys.Apply(keys =>
                                keys.PrimarySharedKey
                                ?? throw new InvalidOperationException(
                                    "Log Analytics did not return a primary shared key."))),
                    },
                },
                VnetConfiguration = new AppInputs.VnetConfigurationArgs
                {
                    InfrastructureSubnetId = ContainerAppsSubnet.Id,
                    Internal = false,
                },
                ZoneRedundant = false,
                Tags = args.Tags,
            },
            childOptions);

        var connectionString = Output.Tuple<string, string>(
                SqlServer.FullyQualifiedDomainName,
                args.SqlAdminPassword)
            .Apply(values =>
                $"Server=tcp:{values.Item1},1433;Initial Catalog={args.Names.SqlDatabase};" +
                $"User ID={args.SqlAdminLogin};Password={values.Item2};" +
                "Persist Security Info=False;MultipleActiveResultSets=False;" +
                "Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");

        DatabaseConnectionSecret = new Secret(
            "catalog-database-connection-secret",
            new SecretArgs
            {
                ResourceGroupName = ResourceGroup.Name,
                VaultName = KeyVault.Name,
                SecretName = "catalog-db",
                Properties = new KeyVaultInputs.SecretPropertiesArgs
                {
                    ContentType = "application/x-sql-connection-string",
                    Value = Output.CreateSecret(connectionString),
                },
                Tags = args.Tags,
            },
            childOptions);

        KeyVaultSecretUrl = Output.Format(
            $"https://{args.Names.KeyVault}.vault.azure.net/secrets/catalog-db");
        ContainerRegistryLoginServer = Output.Format($"{args.Names.ContainerRegistry}.azurecr.io");

        RegisterOutputs(
            new Dictionary<string, object?>
            {
                ["resourceGroupName"] = ResourceGroup.Name,
                ["containerRegistryLoginServer"] = ContainerRegistryLoginServer,
                ["containerAppsEnvironmentId"] = ContainerAppsEnvironment.Id,
                ["managedIdentityId"] = ManagedIdentity.Id,
                ["keyVaultName"] = KeyVault.Name,
                ["sqlServerName"] = SqlServer.Name,
            });
    }

    public ResourceGroup ResourceGroup { get; }

    public VirtualNetwork VirtualNetwork { get; }

    public Subnet ContainerAppsSubnet { get; }

    public Subnet PrivateEndpointsSubnet { get; }

    public Registry ContainerRegistry { get; }

    public Workspace LogAnalytics { get; }

    public Component ApplicationInsights { get; }

    public UserAssignedIdentity ManagedIdentity { get; }

    public Vault KeyVault { get; }

    public Server SqlServer { get; }

    public Database SqlDatabase { get; }

    public PrivateZone SqlPrivateDnsZone { get; }

    public PrivateEndpoint SqlPrivateEndpoint { get; }

    public ManagedEnvironment ContainerAppsEnvironment { get; }

    public Secret DatabaseConnectionSecret { get; }

    public Output<string> KeyVaultSecretUrl { get; }

    public Output<string> ContainerRegistryLoginServer { get; }
}

internal sealed record CatalogFoundationArgs(
    string Location,
    string TenantId,
    CatalogNames Names,
    string SqlAdminLogin,
    Output<string> SqlAdminPassword,
    string SqlDatabaseSku,
    InputMap<string> Tags);
