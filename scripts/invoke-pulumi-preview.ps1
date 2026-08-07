#Requires -Version 7.0

<#
.SYNOPSIS
Runs `pulumi preview` against the already-selected stack, stores the machine readable
digest as pipeline evidence, and fails when durable Catalog infrastructure would be
destroyed or replaced.

.DESCRIPTION
The caller is expected to have run `bootstrap-pulumi.ps1` first, so the Pulumi backend is
already logged in and the `dev` stack is selected. Only the preview is executed here; this
script never calls `pulumi up` and never calls `pulumi destroy`.

A preview that deletes or replaces a protected resource type is a hard failure. Those
resources hold durable state (databases, registries, vaults, identities, telemetry) and an
unattended pipeline must never remove them silently.

Pass -PreviewJsonPath to analyse a previously captured digest without invoking Pulumi.
scripts/tests/preview-guard-check.ps1 uses that mode to verify the guard.
#>

[CmdletBinding(DefaultParameterSetName = 'RunPreview')]
param(
    [Parameter(Mandatory, ParameterSetName = 'RunPreview')][string]$PulumiPath,
    [Parameter(Mandatory, ParameterSetName = 'RunPreview')][string]$EvidencePath,
    [Parameter(Mandatory, ParameterSetName = 'Analyze')][string]$PreviewJsonPath,
    [string]$Label = 'preview',

    # Protected types whose removal is intended by the current change. Retiring a paid
    # resource is a legitimate operation, but it has to be named deliberately rather than
    # discovered in a diff, so the guard stays strict for everything not listed here.
    [string[]]$AllowedDeletionTypes = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Azure resource types whose deletion or replacement destroys durable state or breaks the
# identity of the existing dev environment. Keep this aligned with CatalogFoundation.
$script:ProtectedTypes = @(
    'azure-native:sql:Server'
    'azure-native:sql:Database'
    'azure-native:containerregistry:Registry'
    'azure-native:keyvault:Vault'
    'azure-native:managedidentity:UserAssignedIdentity'
    'azure-native:operationalinsights:Workspace'
    'azure-native:insights:Component'
    'azure-native:app:ManagedEnvironment'
    'azure-native:app:ContainerApp'
    'azure-native:resources:ResourceGroup'
    # The Container Apps environment is delegated into this subnet and cannot survive it
    # being recreated. Property edits such as adding a service endpoint are updates and
    # stay allowed; only a delete or replace is refused.
    'azure-native:network:VirtualNetwork'
    'azure-native:network:Subnet'
)

# `replace` is expanded by the engine into create-replacement/delete-replaced, so all four
# forms have to be treated as destructive.
$script:DestructiveOperations = @('delete', 'replace', 'delete-replaced', 'create-replacement')

function Get-PulumiResourceType {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Urn)

    # URN shape: urn:pulumi:<stack>::<project>::<parent types>$<leaf type>::<name>
    $segments = $Urn -split '::'
    if ($segments.Count -lt 3) {
        return ''
    }

    return ($segments[2] -split '\$')[-1]
}

function Get-ProtectedResourceViolation {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Steps,
        [AllowEmptyCollection()][string[]]$Allowed = @()
    )

    $violations = @()
    foreach ($step in $Steps) {
        if ($step.op -notin $script:DestructiveOperations) {
            continue
        }

        $leafType = Get-PulumiResourceType -Urn ([string]$step.urn)
        if ($leafType -notin $script:ProtectedTypes) {
            continue
        }

        if ($leafType -in $Allowed) {
            Write-Host "  allowed removal: [$($step.op)] $leafType -> $($step.urn)"
            continue
        }

        $violations += "[$($step.op)] $leafType -> $($step.urn)"
    }

    return $violations
}

if ($PSCmdlet.ParameterSetName -eq 'Analyze') {
    $previewText = Get-Content -Raw -LiteralPath $PreviewJsonPath
    $EvidencePath = $PreviewJsonPath
}
else {
    $evidenceDirectory = Split-Path -Parent $EvidencePath
    if (-not [string]::IsNullOrWhiteSpace($evidenceDirectory)) {
        New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
    }

    # --show-sames keeps unchanged resources in the digest. Without it Pulumi emits only
    # the changed steps, so the stored evidence would not show what the stack actually
    # contains and the resource count would be meaningless to a reviewer.
    $previewJson = & $PulumiPath preview --json --show-sames --non-interactive
    if ($LASTEXITCODE -ne 0) {
        $previewJson | Out-Host
        throw "Pulumi preview '$Label' failed."
    }

    $previewText = $previewJson -join "`n"
    $previewText | Set-Content -LiteralPath $EvidencePath -Encoding utf8NoBOM
}

try {
    $preview = $previewText | ConvertFrom-Json -Depth 100
}
catch {
    throw "Pulumi preview '$Label' did not return a parsable JSON digest."
}

$steps = @()
if ($preview.PSObject.Properties.Name -contains 'steps' -and $null -ne $preview.steps) {
    $steps = @($preview.steps)
}

$changed = @($steps | Where-Object { $_.op -ne 'same' })

Write-Host "Pulumi preview '$Label': $($steps.Count) resources evaluated, $($changed.Count) with pending changes."
foreach ($step in $changed) {
    Write-Host "  [$($step.op)] $($step.urn)"
}

# @() keeps an empty result an array; PowerShell would otherwise unwrap it to $null and
# Set-StrictMode would fail on .Count.
$violations = @(Get-ProtectedResourceViolation -Steps $steps -Allowed $AllowedDeletionTypes)
if ($violations.Count -gt 0) {
    Write-Host '##vso[task.logissue type=error]Pulumi preview proposes destroying protected Catalog infrastructure.'
    $violations | ForEach-Object { Write-Host "##vso[task.logissue type=error]$_" }
    throw "Pulumi preview '$Label' proposes $($violations.Count) destructive change(s) to protected resources. Refusing to continue; review the preview evidence and resolve the drift manually."
}

[pscustomobject]@{
    Label = $Label
    EvidencePath = $EvidencePath
    TotalResources = $steps.Count
    PendingChanges = $changed.Count
    ChangeSummary = $preview.changeSummary
}
