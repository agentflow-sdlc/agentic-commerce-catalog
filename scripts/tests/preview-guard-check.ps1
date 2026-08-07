#Requires -Version 7.0

<#
.SYNOPSIS
Self-check for the Pulumi preview destruction guard in scripts/invoke-pulumi-preview.ps1.

.DESCRIPTION
The guard is what stops an unattended `pulumi up` from deleting Azure SQL, the registry,
the vault, or the container app. It has to fail on destructive previews and pass on benign
ones, so both directions are asserted here against fixture digests. No Pulumi backend, no
Azure login, and no network access are required.

Run with: pwsh ./scripts/tests/preview-guard-check.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$guard = Join-Path $PSScriptRoot '..\invoke-pulumi-preview.ps1'
$workingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "catalog-preview-guard-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null

function New-PreviewFixture {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][object[]]$Steps
    )

    $path = Join-Path $workingDirectory "$Name.json"
    @{ steps = $Steps; changeSummary = @{ same = $Steps.Count } } |
        ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath $path -Encoding utf8NoBOM
    return $path
}

function Assert-GuardAccepts {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Because,
        [string[]]$Allowed = @()
    )

    try {
        & $guard -PreviewJsonPath $Path -Label 'self-check' -AllowedDeletionTypes $Allowed | Out-Null
    }
    catch {
        throw "FAILED: guard rejected a safe preview ($Because). $($_.Exception.Message)"
    }

    Write-Host "  ok: $Because"
}

function Assert-GuardRejects {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Because,
        [string[]]$Allowed = @()
    )

    try {
        & $guard -PreviewJsonPath $Path -Label 'self-check' -AllowedDeletionTypes $Allowed | Out-Null
    }
    catch {
        Write-Host "  ok: $Because"
        return
    }

    throw "FAILED: guard accepted a destructive preview ($Because)."
}

$sqlServerUrn = 'urn:pulumi:dev::catalog::agentflow:catalog:CatalogFoundation$azure-native:sql:Server::catalog-sql-server'
$containerAppUrn = 'urn:pulumi:dev::catalog::agentflow:catalog:CatalogWorkload$azure-native:app:ContainerApp::catalog-api'
$roleAssignmentUrn = 'urn:pulumi:dev::catalog::agentflow:catalog:CatalogWorkload$azure-native:authorization:RoleAssignment::catalog-acr-pull'
$registryUrn = 'urn:pulumi:dev::catalog::agentflow:catalog:CatalogFoundation$azure-native:containerregistry:Registry::catalog-registry'

try {
    Write-Host 'Pulumi preview guard self-check'

    Assert-GuardAccepts `
        -Path (New-PreviewFixture -Name 'no-changes' -Steps @(
            @{ op = 'same'; urn = $sqlServerUrn }
            @{ op = 'same'; urn = $containerAppUrn })) `
        -Because 'an unchanged stack is allowed'

    Assert-GuardAccepts `
        -Path (New-PreviewFixture -Name 'workload-update' -Steps @(
            @{ op = 'same'; urn = $sqlServerUrn }
            @{ op = 'update'; urn = $containerAppUrn })) `
        -Because 'updating the container app image is allowed'

    Assert-GuardAccepts `
        -Path (New-PreviewFixture -Name 'unprotected-replace' -Steps @(
            @{ op = 'replace'; urn = $roleAssignmentUrn })) `
        -Because 'replacing an unprotected resource is allowed'

    Assert-GuardRejects `
        -Path (New-PreviewFixture -Name 'sql-delete' -Steps @(
            @{ op = 'delete'; urn = $sqlServerUrn })) `
        -Because 'deleting Azure SQL is refused'

    Assert-GuardRejects `
        -Path (New-PreviewFixture -Name 'sql-replace' -Steps @(
            @{ op = 'replace'; urn = $sqlServerUrn })) `
        -Because 'replacing Azure SQL is refused'

    Assert-GuardRejects `
        -Path (New-PreviewFixture -Name 'container-app-delete-replaced' -Steps @(
            @{ op = 'delete-replaced'; urn = $containerAppUrn })) `
        -Because 'a replacement delete of the container app is refused'

    # Retiring a paid resource on purpose has to be possible, but only when named.
    Assert-GuardAccepts `
        -Path (New-PreviewFixture -Name 'registry-delete-allowed' -Steps @(
            @{ op = 'delete'; urn = $registryUrn })) `
        -Because 'deleting the registry is allowed when explicitly named' `
        -Allowed @('azure-native:containerregistry:Registry')

    Assert-GuardRejects `
        -Path (New-PreviewFixture -Name 'registry-delete-unnamed' -Steps @(
            @{ op = 'delete'; urn = $registryUrn })) `
        -Because 'deleting the registry is refused when not named'

    Assert-GuardRejects `
        -Path (New-PreviewFixture -Name 'allowance-does-not-leak' -Steps @(
            @{ op = 'delete'; urn = $sqlServerUrn })) `
        -Because 'allowing the registry does not also allow Azure SQL' `
        -Allowed @('azure-native:containerregistry:Registry')

    Write-Host 'Pulumi preview guard self-check passed.'
}
finally {
    Remove-Item -LiteralPath $workingDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
