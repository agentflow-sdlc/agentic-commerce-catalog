using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.Authorization;
using AppInputs = Pulumi.AzureNative.App.Inputs;

namespace Catalog.Infrastructure.Components;

internal sealed class CatalogWorkload : ComponentResource
{
    private const string AcrPullRoleId = "7f951dda-4ed3-4680-a7ca-43fe172d538d";
    private const string KeyVaultSecretsUserRoleId = "4633458b-17de-408a-b874-0445c86b69e6";
    private const string MonitoringMetricsPublisherRoleId = "3913510d-42f4-4e42-8a64-420c390055eb";

    public CatalogWorkload(
        string name,
        CatalogWorkloadArgs args,
        ComponentResourceOptions? options = null)
        : base("agentflow:catalog:CatalogWorkload", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        // Only meaningful when an Azure Container Registry exists. Under the free-first
        // profile images are pulled anonymously from public GHCR packages, so there is
        // nothing to grant AcrPull on and no registry credential to hold.
        var acrPull = args.Foundation.ContainerRegistry is null
            ? null
            : CreateRoleAssignment(
                "catalog-acr-pull",
                args.Foundation.ContainerRegistry.Id,
                args.Foundation.ManagedIdentity.PrincipalId,
                args.SubscriptionId,
                AcrPullRoleId,
                childOptions);

        var keyVaultSecretsUser = CreateRoleAssignment(
            "catalog-key-vault-secrets-user",
            args.Foundation.KeyVault.Id,
            args.Foundation.ManagedIdentity.PrincipalId,
            args.SubscriptionId,
            KeyVaultSecretsUserRoleId,
            childOptions);

        var monitoringPublisher = CreateRoleAssignment(
            "catalog-monitoring-metrics-publisher",
            args.Foundation.ApplicationInsights.Id,
            args.Foundation.ManagedIdentity.PrincipalId,
            args.SubscriptionId,
            MonitoringMetricsPublisherRoleId,
            childOptions);

        var identity = new AppInputs.ManagedServiceIdentityArgs
        {
            Type = "UserAssigned",
            UserAssignedIdentities = [args.Foundation.ManagedIdentity.Id],
        };

        // Empty when pulling public GHCR images: Container Apps pulls anonymously, so no
        // registry credential, no personal access token and no stored secret are needed.
        var registries = args.Foundation.ContainerRegistryLoginServer is null
            ? Array.Empty<AppInputs.RegistryCredentialsArgs>()
            :
            [
                new AppInputs.RegistryCredentialsArgs
                {
                    Server = args.Foundation.ContainerRegistryLoginServer,
                    Identity = args.Foundation.ManagedIdentity.Id,
                },
            ];

        var dependencies = new List<Resource>
        {
            keyVaultSecretsUser,
            monitoringPublisher,
            args.Foundation.DatabaseConnectionSecret,
        };

        if (acrPull is not null)
        {
            dependencies.Add(acrPull);
        }

        var databaseSecret = new AppInputs.SecretArgs
        {
            Name = "catalog-db",
            KeyVaultUrl = args.Foundation.KeyVaultSecretUrl,
            Identity = args.Foundation.ManagedIdentity.Id,
        };

        var commonEnvironment = new[]
        {
            new AppInputs.EnvironmentVarArgs
            {
                Name = "ConnectionStrings__CatalogDb",
                SecretRef = "catalog-db",
            },
            new AppInputs.EnvironmentVarArgs
            {
                Name = "AZURE_CLIENT_ID",
                Value = args.Foundation.ManagedIdentity.ClientId,
            },
        };

        CatalogApi = new ContainerApp(
            "catalog-api",
            new ContainerAppArgs
            {
                ResourceGroupName = args.Foundation.ResourceGroup.Name,
                ContainerAppName = args.Names.CatalogApi,
                Location = args.Foundation.ResourceGroup.Location,
                EnvironmentId = args.Foundation.ContainerAppsEnvironment.Id,
                Identity = identity,
                Configuration = new AppInputs.ConfigurationArgs
                {
                    ActiveRevisionsMode = "Single",
                    Registries = registries,
                    Secrets = [databaseSecret],
                    Ingress = new AppInputs.IngressArgs
                    {
                        External = true,
                        AllowInsecure = false,
                        TargetPort = 8080,
                        TargetPortHttpScheme = "http",
                        Transport = "Auto",
                    },
                },
                Template = new AppInputs.TemplateArgs
                {
                    Containers =
                    [
                        new AppInputs.ContainerArgs
                        {
                            Name = "catalog-api",
                            Image = args.ApiImage,
                            Env =
                            [
                                .. commonEnvironment,
                                new AppInputs.EnvironmentVarArgs
                                {
                                    Name = "ApplicationInsights__ConnectionString",
                                    Value = args.Foundation.ApplicationInsights.ConnectionString,
                                },
                            ],
                            Resources = new AppInputs.ContainerResourcesArgs
                            {
                                Cpu = 0.25,
                                Memory = "0.5Gi",
                            },
                            Probes =
                            [
                                CreateHttpProbe("Startup", 5, 10),
                                CreateHttpProbe("Liveness", 10, 3),
                                CreateHttpProbe("Readiness", 5, 3),
                            ],
                        },
                    ],
                    Scale = new AppInputs.ScaleArgs
                    {
                        MinReplicas = 0,
                        MaxReplicas = 2,
                    },
                },
                Tags = args.Tags,
            },
            new CustomResourceOptions
            {
                Parent = this,
                DependsOn = dependencies.ToArray(),
            });

        DatabaseMigratorJob = new Job(
            "catalog-database-migrator-job",
            new JobArgs
            {
                ResourceGroupName = args.Foundation.ResourceGroup.Name,
                JobName = args.Names.DatabaseMigratorJob,
                Location = args.Foundation.ResourceGroup.Location,
                EnvironmentId = args.Foundation.ContainerAppsEnvironment.Id,
                Identity = identity,
                Configuration = new AppInputs.JobConfigurationArgs
                {
                    TriggerType = "Manual",
                    Registries = registries,
                    Secrets = [databaseSecret],
                    ReplicaRetryLimit = 1,
                    ReplicaTimeout = 600,
                    ManualTriggerConfig = new AppInputs.JobConfigurationManualTriggerConfigArgs
                    {
                        Parallelism = 1,
                        ReplicaCompletionCount = 1,
                    },
                },
                Template = new AppInputs.JobTemplateArgs
                {
                    Containers =
                    [
                        new AppInputs.ContainerArgs
                        {
                            Name = "catalog-database-migrator",
                            Image = args.MigratorImage,
                            Env = commonEnvironment,
                            Resources = new AppInputs.ContainerResourcesArgs
                            {
                                Cpu = 0.25,
                                Memory = "0.5Gi",
                            },
                        },
                    ],
                },
                Tags = args.Tags,
            },
            new CustomResourceOptions
            {
                Parent = this,
                // The job does not publish metrics, so it needs the same dependencies
                // minus the monitoring role assignment.
                DependsOn = dependencies
                    .Where(dependency => dependency != monitoringPublisher)
                    .ToArray(),
            });

        CatalogUrl = CatalogApi.Configuration.Apply(configuration =>
            $"https://{configuration?.Ingress?.Fqdn
                ?? throw new InvalidOperationException(
                    "Catalog ingress did not return a fully qualified domain name.")}");

        RegisterOutputs(
            new Dictionary<string, object?>
            {
                ["catalogUrl"] = CatalogUrl,
                ["catalogApiName"] = CatalogApi.Name,
                ["databaseMigratorJobName"] = DatabaseMigratorJob.Name,
            });
    }

    public ContainerApp CatalogApi { get; }

    public Job DatabaseMigratorJob { get; }

    public Output<string> CatalogUrl { get; }

    private static AppInputs.ContainerAppProbeArgs CreateHttpProbe(
        string type,
        int periodSeconds,
        int failureThreshold) => new()
        {
            Type = type,
            HttpGet = new AppInputs.ContainerAppProbeHttpGetArgs
            {
                Path = "/health",
                Port = 8080,
                Scheme = "HTTP",
            },
            InitialDelaySeconds = 3,
            PeriodSeconds = periodSeconds,
            TimeoutSeconds = 3,
            FailureThreshold = failureThreshold,
            SuccessThreshold = 1,
        };

    private static RoleAssignment CreateRoleAssignment(
        string logicalName,
        Input<string> scope,
        Input<string> principalId,
        string subscriptionId,
        string roleId,
        CustomResourceOptions options) => new(
            logicalName,
            new RoleAssignmentArgs
            {
                Scope = scope,
                PrincipalId = principalId,
                PrincipalType = "ServicePrincipal",
                RoleDefinitionId =
                    $"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{roleId}",
                RoleAssignmentName = DeterministicGuid.Create(
                    $"{subscriptionId}:{logicalName}:{roleId}"),
            },
            options);
}

internal sealed record CatalogWorkloadArgs(
    string SubscriptionId,
    CatalogNames Names,
    CatalogFoundation Foundation,
    string ApiImage,
    string MigratorImage,
    InputMap<string> Tags);
