#Requires -Version 7.0

<#
.SYNOPSIS
Self-check for the internal Catalog smoke transport in scripts/azure-containerapp-job.ps1.

.DESCRIPTION
Catalog ingress is internal, so deployment verification depends entirely on this transport.
If it silently reported success the pipeline would stop verifying anything, so both
directions are asserted: a successful execution returns its identity, and every unsuccessful
outcome throws. The temporary job must also be removed even when the run fails, or a retried
pipeline would leave orphaned jobs behind.

The Azure CLI is replaced by a scripted fake, so no Azure login, no subscription and no
network access are required.

Run with: pwsh ./scripts/tests/internal-smoke-check.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot '..\azure-containerapp-job.ps1')

$script:Calls = @()
$script:ProvisioningState = 'Succeeded'
$script:ExecutionStatus = 'Succeeded'
$script:ExecutionName = 'catalog-smoke-abc123'
$script:EnvironmentExitCode = 0

# A plain scriptblock, deliberately not GetNewClosure(): a closure would capture its own
# scope and the recorded calls would never reach the assertions below.
$script:AzFake = {
    param([string[]]$Arguments)

    $script:Calls += , $Arguments
    $joined = $Arguments -join ' '

    if ($joined -like 'containerapp show*') {
        return [pscustomobject]@{
            ExitCode = $script:EnvironmentExitCode
            Output = @('{"environmentId":"/subscriptions/s/resourceGroups/rg/providers/Microsoft.App/managedEnvironments/cae","location":"eastus2"}')
        }
    }
    if ($joined -like 'rest --method put*') {
        return [pscustomobject]@{ ExitCode = 0; Output = @() }
    }
    if ($joined -like 'containerapp job show*') {
        return [pscustomobject]@{ ExitCode = 0; Output = @($script:ProvisioningState) }
    }
    if ($joined -like 'containerapp job start*') {
        return [pscustomobject]@{ ExitCode = 0; Output = @($script:ExecutionName) }
    }
    if ($joined -like 'containerapp job execution show*') {
        return [pscustomobject]@{ ExitCode = 0; Output = @($script:ExecutionStatus) }
    }
    if ($joined -like 'containerapp job logs show*') {
        return [pscustomobject]@{
            ExitCode = 0
            Output = @('GET /health failed', 'ConnectionStrings__CatalogDb=Server=tcp:x;Password=hunter2')
        }
    }
    if ($joined -like 'containerapp job delete*') {
        return [pscustomobject]@{ ExitCode = 0; Output = @() }
    }

    throw "The fake Azure CLI received an unexpected call: $joined"
}

function Invoke-Transport {
    param(
        [string]$ProvisioningState = 'Succeeded',
        [string]$ExecutionStatus = 'Succeeded',
        [string]$ExecutionName = 'catalog-smoke-abc123',
        [int]$EnvironmentExitCode = 0
    )

    $script:Calls = @()
    $script:ProvisioningState = $ProvisioningState
    $script:ExecutionStatus = $ExecutionStatus
    $script:ExecutionName = $ExecutionName
    $script:EnvironmentExitCode = $EnvironmentExitCode

    return Invoke-CatalogContainerAppJob `
        -ResourceGroup 'rg-agentic-catalog-dev-070775' `
        -ContainerApp 'ca-catalog-api-070775' `
        -Image 'mcr.microsoft.com/powershell:7.5-azurelinux-3.0' `
        -Command 'pwsh' `
        -Arguments @('-NoProfile', '-EncodedCommand', 'ZWNobyBoaQ==') `
        -JobName 'caj-catalog-smoke-070775' `
        -MaxAttempts 3 `
        -DelaySeconds 0 `
        -CommandRunner $script:AzFake `
        -SleepAction { param([int]$Seconds) }
}

function Test-CalledWith {
    param([Parameter(Mandatory)][string]$Pattern)

    return @($script:Calls | Where-Object { ($_ -join ' ') -like $Pattern }).Count -gt 0
}

Write-Host 'Catalog internal smoke transport self-check'

# 1. A healthy execution returns the identity the evidence records.
$result = Invoke-Transport
if ($result.Status -ne 'Succeeded') { throw 'FAILED: a successful execution did not report Succeeded.' }
if ($result.ExecutionName -ne 'catalog-smoke-abc123') { throw 'FAILED: the execution name was not returned.' }
if (-not (Test-CalledWith -Pattern 'containerapp job delete*')) {
    throw 'FAILED: the temporary job was not deleted after a successful run.'
}
Write-Host '  ok: a successful execution returns its job and execution identity'

# PUT converges rather than colliding, which is what makes a retried pipeline run safe.
if (-not (Test-CalledWith -Pattern 'rest --method put*')) {
    throw 'FAILED: the job was not created idempotently with PUT.'
}
Write-Host '  ok: the job is created with an idempotent PUT'

# 2. A failed execution must fail the pipeline, never pass quietly.
$failed = $false
try {
    Invoke-Transport -ExecutionStatus 'Failed' | Out-Null
}
catch {
    $failed = $true
    if ($_.Exception.Message -notmatch 'Failed') { throw "FAILED: unexpected failure message: $($_.Exception.Message)" }
    # Diagnostics are useful; leaking a connection string into a public build log is not.
    if ($_.Exception.Message -match 'hunter2' -or $_.Exception.Message -match 'Server=tcp:') {
        throw 'FAILED: smoke failure diagnostics leaked credential material.'
    }
    if ($_.Exception.Message -notmatch '\[REDACTED\]') {
        throw 'FAILED: smoke failure diagnostics were not redacted.'
    }
}
if (-not $failed) { throw 'FAILED: a failed execution did not throw.' }
if (-not (Test-CalledWith -Pattern 'containerapp job delete*')) {
    throw 'FAILED: the temporary job survived a failed run.'
}
Write-Host '  ok: a failed execution throws, redacts its logs and still cleans up'

# 3. Provisioning that never succeeds is a failure, not an infinite wait.
$failed = $false
try { Invoke-Transport -ProvisioningState 'Failed' | Out-Null }
catch { $failed = $true }
if (-not $failed) { throw 'FAILED: a job that failed to provision did not throw.' }
Write-Host '  ok: a job that cannot provision throws'

# 4. A start that returns no execution cannot be reported as verified.
$failed = $false
try { Invoke-Transport -ExecutionName '' | Out-Null }
catch { $failed = $true }
if (-not $failed) { throw 'FAILED: a missing execution name did not throw.' }
Write-Host '  ok: a start without an execution name throws'

# 5. An unreachable management plane is a failure, not a skipped verification.
$failed = $false
try { Invoke-Transport -EnvironmentExitCode 1 | Out-Null }
catch { $failed = $true }
if (-not $failed) { throw 'FAILED: an Azure CLI failure did not throw.' }
Write-Host '  ok: an Azure management-plane failure throws'

# 6. Deployment verification must never mutate Catalog data.
$smoke = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '..\internal-smoke-dev.ps1')
foreach ($verb in @('POST', 'PATCH', 'PUT', 'DELETE')) {
    if ($smoke -match "-Method\s+$verb") {
        throw "FAILED: the internal smoke script issues a $verb request; deployment smoke must be read-only."
    }
}
Write-Host '  ok: the internal smoke script issues no mutating request'

# 7. The smoke must refuse a public endpoint rather than silently validating one.
if ($smoke -notmatch '\\\.internal\\\.') {
    throw 'FAILED: the internal smoke script does not assert an internal Container Apps FQDN.'
}
Write-Host '  ok: the internal smoke script refuses a non-internal endpoint'

Write-Host 'Catalog internal smoke transport self-check passed.'
