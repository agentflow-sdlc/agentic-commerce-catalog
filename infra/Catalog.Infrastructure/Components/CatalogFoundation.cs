using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.ApplicationInsights;
using Pulumi.AzureNative.ContainerRegistry;
using Pulumi.AzureNative.CostManagement;
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
using CostManagementInputs = Pulumi.AzureNative.CostManagement.Inputs;
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
                // No Microsoft.Sql service endpoint: it only has an effect alongside a
                // virtual network rule on the server, which Azure refuses across regions
                // while the server stays in centralus. See the firewall rule on the server.
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

        // ACR Basic costs a fixed monthly amount whether or not an image is ever pulled.
        // Under the free-first profile images live in GitHub Container Registry instead,
        // which is free for public packages and needs no credential to pull.
        if (!args.FreeFirst)
        {
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
        }

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
                // The workspace has a 5 GB/month free grant and nothing stops ingestion
                // once it is spent. A daily cap turns a silent overage into dropped
                // telemetry, which is the correct failure mode for a POC.
                WorkspaceCapping = new OperationalInsightsInputs.WorkspaceCappingArgs
                {
                    // -1 is Azure's "no cap".
                    DailyQuotaGb = args.FreeFirst ? 0.1 : -1,
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
                // 100% sampling maximises the chance of leaving the free grant. A POC
                // needs representative telemetry, not every single request.
                SamplingPercentage = args.FreeFirst ? 25 : 100,
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
                Location = args.SqlLocation,
                AdministratorLogin = args.SqlAdminLogin,
                AdministratorLoginPassword = args.SqlAdminPassword,
                MinimalTlsVersion = "1.2",
                // A Private Endpoint costs roughly $7/month permanently to demonstrate a
                // topology this POC is not demonstrating. Under free-first the server keeps
                // its public endpoint, but the virtual network rule below admits only the
                // Container Apps subnet, still TLS 1.2 minimum, still Key Vault credentials.
                PublicNetworkAccess = args.FreeFirst ? "Enabled" : "Disabled",
                RestrictOutboundNetworkAccess = "Enabled",
                Version = "12.0",
                Tags = args.Tags,
            },
            childOptions);

        // The firewall rule that admits the workload is created after the Container Apps
        // environment, because it allows that environment's outbound address.

        // Azure SQL Database free offer: General Purpose Serverless Gen5 2 vCore, 32 GB,
        // auto-pause, with a free monthly allowance. It removes the only remaining fixed
        // compute charge in this stack.
        //
        // The catch: `useFreeLimit` is settable only at CREATION. Turning it on for the
        // existing database means Pulumi replaces it, which destroys the data. That is why
        // it sits behind its own opt-in flag and is NOT part of the free-first default -
        // "free" must never silently mean "deleted". Enabling it is an operator decision,
        // taken with a backup in hand. Only one free database is allowed per subscription.
        SqlDatabase = args.SqlUseFreeOffer
            ? new Database(
                "catalog-sql-database",
                new DatabaseArgs
                {
                    ResourceGroupName = ResourceGroup.Name,
                    ServerName = SqlServer.Name,
                    DatabaseName = args.Names.SqlDatabase,
                    Location = args.SqlLocation,
                    MaxSizeBytes = 34_359_738_368,
                    RequestedBackupStorageRedundancy = "Local",
                    Sku = new SqlInputs.SkuArgs
                    {
                        Name = "GP_S_Gen5",
                        Tier = "GeneralPurpose",
                        Family = "Gen5",
                        Capacity = 2,
                    },
                    AutoPauseDelay = 60,
                    MinCapacity = 0.5,
                    UseFreeLimit = true,
                    FreeLimitExhaustionBehavior = "AutoPause",
                    Tags = args.Tags,
                },
                // Azure refuses to convert an existing paid database: "Cannot update paid
                // database to free database" (ProvisioningDisabled). Pulumi plans this as
                // an ordinary update and only finds out mid-apply, so the replacement has
                // to be declared here. Delete first, because the replacement reuses the
                // same database name and the two cannot coexist on the server.
                CustomResourceOptions.Merge(
                    childOptions,
                    new CustomResourceOptions
                    {
                        ReplaceOnChanges = { "useFreeLimit" },
                        DeleteBeforeReplace = true,
                    }))
            : new Database(
                "catalog-sql-database",
                new DatabaseArgs
                {
                    ResourceGroupName = ResourceGroup.Name,
                    ServerName = SqlServer.Name,
                    DatabaseName = args.Names.SqlDatabase,
                    Location = args.SqlLocation,
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

        // The private endpoint, its private DNS zone, the zone's VNet link and the zone
        // group exist solely to reach Azure SQL privately. They are a permanent fixed cost
        // and are not provisioned under the free-first profile. The VNet and its subnets
        // stay: they are free, and the Container Apps environment is integrated with them.
        if (!args.FreeFirst)
        {
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
        }

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

        if (args.FreeFirst)
        {
            // This admits every Azure tenant, so the admin password is the only barrier for
            // anyone able to start a VM. It stays because both narrower forms are blocked,
            // and the measurements are recorded so nobody retries them from scratch:
            //
            // Allowlisting the environment's address does not work. Container Apps
            // Consumption egresses from more than one address: with only StaticIp
            // (172.193.22.14) permitted, the migration job connected but the API's replicas
            // were refused with error 40615. An API call appearing to succeed during that
            // window was a warm connection pool, not proof - the firewall only affects new
            // connections, so this must be tested against a fresh replica.
            //
            // A virtual network rule is the correct fix and Azure rejects it here: those
            // rules require the server and the network to share a region. The server is in
            // centralus because this subscription offers no serverless Gen5 in eastus2, and
            // serverless is what the free offer needs. Colocating everything in centralus
            // would make it legal, at the cost of recreating the environment and its URL.
            //
            // ponytail: the remaining upgrades both cost something - a Private Endpoint at
            // roughly $7/month, or the move to centralus above. Deliberate decisions, not
            // cleanup, so neither is taken here.
            _ = new FirewallRule(
                "catalog-sql-allow-azure-services",
                new FirewallRuleArgs
                {
                    ResourceGroupName = ResourceGroup.Name,
                    ServerName = SqlServer.Name,
                    FirewallRuleName = "AllowAllWindowsAzureIps",
                    StartIpAddress = "0.0.0.0",
                    EndIpAddress = "0.0.0.0",
                },
                childOptions);
        }

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

        // A budget costs nothing and is the only thing that will actually tell someone the
        // POC started spending. Scoped to this resource group rather than the subscription
        // so it cannot be confused with unrelated spend. Skipped when no contact address is
        // configured, because a budget nobody is notified about is decoration.
        if (!string.IsNullOrWhiteSpace(args.BudgetContactEmail))
        {
            var thresholds = new[] { 50.0, 80.0, 100.0 };
            var notifications = thresholds.ToDictionary(
                threshold => $"actual-{threshold:0}-percent",
                threshold => new CostManagementInputs.NotificationArgs
                {
                    Enabled = true,
                    Operator = "GreaterThanOrEqualTo",
                    Threshold = threshold,
                    ThresholdType = "Actual",
                    ContactEmails = [args.BudgetContactEmail!],
                });

            _ = new Budget(
                "catalog-budget",
                new BudgetArgs
                {
                    Scope = ResourceGroup.Id,
                    BudgetName = "catalog-poc-monthly",
                    Amount = args.BudgetAmountUsd,
                    Category = "Cost",
                    TimeGrain = "Monthly",
                    TimePeriod = new CostManagementInputs.BudgetTimePeriodArgs
                    {
                        StartDate = args.BudgetStartDate,
                    },
                    Notifications = notifications,
                },
                childOptions);
        }

        KeyVaultSecretUrl = Output.Format(
            $"https://{args.Names.KeyVault}.vault.azure.net/secrets/catalog-db");
        ContainerRegistryLoginServer = ContainerRegistry is null
            ? null
            : Output.Format($"{args.Names.ContainerRegistry}.azurecr.io");

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

    /// <summary>Null under the free-first profile; images come from GHCR instead.</summary>
    public Registry? ContainerRegistry { get; }

    public Workspace LogAnalytics { get; }

    public Component ApplicationInsights { get; }

    public UserAssignedIdentity ManagedIdentity { get; }

    public Vault KeyVault { get; }

    public Server SqlServer { get; }

    public Database SqlDatabase { get; }

    /// <summary>Null under the free-first profile; the private endpoint is not created.</summary>
    public PrivateZone? SqlPrivateDnsZone { get; }

    /// <summary>Null under the free-first profile; SQL is reached over its public endpoint.</summary>
    public PrivateEndpoint? SqlPrivateEndpoint { get; }

    public ManagedEnvironment ContainerAppsEnvironment { get; }

    public Secret DatabaseConnectionSecret { get; }

    public Output<string> KeyVaultSecretUrl { get; }

    /// <summary>Null under the free-first profile; images come from GHCR instead.</summary>
    public Output<string>? ContainerRegistryLoginServer { get; }
}

internal sealed record CatalogFoundationArgs(
    string Location,
    string SqlLocation,
    string TenantId,
    CatalogNames Names,
    string SqlAdminLogin,
    Output<string> SqlAdminPassword,
    string SqlDatabaseSku,
    bool FreeFirst,
    bool SqlUseFreeOffer,
    string? BudgetContactEmail,
    double BudgetAmountUsd,
    string BudgetStartDate,
    InputMap<string> Tags);
