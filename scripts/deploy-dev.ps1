#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$SkipLocalValidation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$infrastructureDirectory = Join-Path $repositoryRoot 'infra\Catalog.Infrastructure'
$context = & (Join-Path $PSScriptRoot 'bootstrap-pulumi.ps1') -PassThru
$previousLocation = Get-Location

try {
    if (-not $SkipLocalValidation) {
        Set-Location -LiteralPath $repositoryRoot
        & dotnet restore Catalog.sln
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
        & dotnet build Catalog.sln --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
        & dotnet test Catalog.sln --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
        & dotnet format Catalog.sln --verify-no-changes --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet format verification failed.' }
        & dotnet build infra/Catalog.Infrastructure/Catalog.Infrastructure.csproj --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Pulumi C# build failed.' }
    }

    Set-Location -LiteralPath $infrastructureDirectory
    $workloadEnabled = (& $context.PulumiPath config get catalog:deployWorkload 2>$null) -eq 'true'
    if (-not $workloadEnabled) {
        & $context.PulumiPath config set catalog:deployWorkload false --non-interactive
        & $context.PulumiPath preview --non-interactive --diff
        if ($LASTEXITCODE -ne 0) { throw 'Pulumi foundation preview failed.' }
        & $context.PulumiPath up --yes --non-interactive
        if ($LASTEXITCODE -ne 0) { throw 'Pulumi foundation deployment failed.' }
    }

    $registryName = & $context.PulumiPath stack output containerRegistryName
    $registryLoginServer = & $context.PulumiPath stack output containerRegistryLoginServer
    $commitSha = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
    $buildTimestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')
    $imageTag = "$commitSha-$buildTimestamp"
    $apiRepository = 'catalog-api'
    $migratorRepository = 'catalog-database-migrator'

    Set-Location -LiteralPath $repositoryRoot
    & az acr build `
        --registry $registryName `
        --image "${apiRepository}:$imageTag" `
        --file Dockerfile `
        --platform linux/amd64 `
        .
    if ($LASTEXITCODE -ne 0) { throw 'Catalog API ACR build failed.' }

    & az acr build `
        --registry $registryName `
        --image "${migratorRepository}:$imageTag" `
        --file src/Catalog.DatabaseMigrator/Dockerfile `
        --platform linux/amd64 `
        .
    if ($LASTEXITCODE -ne 0) { throw 'Catalog Database Migrator ACR build failed.' }

    & az acr repository update `
        --name $registryName `
        --image "${apiRepository}:$imageTag" `
        --write-enabled false `
        --delete-enabled false `
        --output none
    if ($LASTEXITCODE -ne 0) { throw 'Catalog API image tag could not be made immutable.' }

    & az acr repository update `
        --name $registryName `
        --image "${migratorRepository}:$imageTag" `
        --write-enabled false `
        --delete-enabled false `
        --output none
    if ($LASTEXITCODE -ne 0) { throw 'Catalog Database Migrator image tag could not be made immutable.' }

    $apiImage = "${registryLoginServer}/${apiRepository}:$imageTag"
    $migratorImage = "${registryLoginServer}/${migratorRepository}:$imageTag"

    Set-Location -LiteralPath $infrastructureDirectory
    & $context.PulumiPath config set catalog:apiImage $apiImage --non-interactive
    & $context.PulumiPath config set catalog:migratorImage $migratorImage --non-interactive
    & $context.PulumiPath config set catalog:deployWorkload true --non-interactive
    & $context.PulumiPath preview --non-interactive --diff
    if ($LASTEXITCODE -ne 0) { throw 'Pulumi workload preview failed.' }
    & $context.PulumiPath up --yes --non-interactive
    if ($LASTEXITCODE -ne 0) { throw 'Pulumi workload deployment failed.' }

    $migration = & (Join-Path $PSScriptRoot 'run-migrations-dev.ps1')
    if ($migration.Status -ne 'Succeeded') {
        throw 'The database migrator job did not succeed.'
    }

    # Catalog ingress is internal, so verification runs inside the Container Apps Environment
    # rather than reaching the service from here.
    $smokeTests = & (Join-Path $PSScriptRoot 'internal-smoke-dev.ps1')

    [pscustomobject]@{
        SubscriptionName = $context.SubscriptionName
        Location = $context.Location
        BackendUrl = $context.BackendUrl
        Stack = $context.Stack
        ApiImage = $apiImage
        MigratorImage = $migratorImage
        MigrationExecution = $migration.ExecutionName
        MigrationStatus = $migration.Status
        CatalogInternalUrl = $smokeTests.target
        SmokeTransport = $smokeTests.transport
        SmokeExecution = $smokeTests.execution
        SmokeStatus = $smokeTests.status
        SmokeChecks = $smokeTests.validatedChecks
    }
}
finally {
    Set-Location -LiteralPath $previousLocation
}
