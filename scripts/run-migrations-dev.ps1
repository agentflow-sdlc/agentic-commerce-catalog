#Requires -Version 7.0

[CmdletBinding()]
param(
    [ValidateRange(1, 60)]
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$context = & (Join-Path $PSScriptRoot 'bootstrap-pulumi.ps1') -PassThru
$infrastructureDirectory = Join-Path $PSScriptRoot '..\infra\Catalog.Infrastructure'
$previousLocation = Get-Location

try {
    Set-Location -LiteralPath $infrastructureDirectory
    $resourceGroup = & $context.PulumiPath stack output resourceGroupName
    $jobName = & $context.PulumiPath stack output databaseMigratorJobName
    if ([string]::IsNullOrWhiteSpace($jobName)) {
        throw 'The database migrator job is not deployed.'
    }

    $execution = & az containerapp job start `
        --resource-group $resourceGroup `
        --name $jobName `
        --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw 'Azure rejected the database migrator job start operation.'
    }

    $executionName = $execution.name
    $deadline = [DateTimeOffset]::UtcNow.AddMinutes($TimeoutMinutes)
    $status = 'Running'

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 10
        $status = & az containerapp job execution show `
            --resource-group $resourceGroup `
            --name $jobName `
            --job-execution-name $executionName `
            --query properties.status `
            --output tsv

        Write-Host "Migration execution status: $status"
        if ($status -in @('Succeeded', 'Failed', 'Stopped', 'Degraded')) {
            break
        }
    }

    if ($status -ne 'Succeeded') {
        Write-Host 'Collecting the last safe migration log lines.'
        $logs = & az containerapp job logs show `
            --resource-group $resourceGroup `
            --name $jobName `
            --execution $executionName `
            --tail 100 2>&1
        $safeLogs = $logs `
            -replace '(?i)(password|pwd)=[^;\s]+', '$1=[REDACTED]' `
            -replace '(?i)(connectionstrings?__[A-Za-z0-9_]+)\s*=\s*\S+', '$1=[REDACTED]'
        $safeLogs | Write-Host
        throw "Database migration execution '$executionName' ended with status '$status'."
    }

    [pscustomobject]@{
        JobName = $jobName
        ExecutionName = $executionName
        Status = $status
    }
}
finally {
    Set-Location -LiteralPath $previousLocation
}
