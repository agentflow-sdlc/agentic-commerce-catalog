#Requires -Version 7.0

<#
.SYNOPSIS
Runs a one-off command inside the Catalog Container Apps Environment as a manual
Container Apps Job.

.DESCRIPTION
Catalog's API has internal-only ingress, so nothing outside the Container Apps Environment
can reach it -- including a GitHub-hosted runner. This is how CI still verifies the deployed
service objectively: the runner uses the Azure management plane (already authenticated with
OIDC) to create, start and poll a manual job, and the actual HTTP requests happen inside the
environment where the internal FQDN resolves.

Adapted from the equivalent AgentFlow platform helper, trimmed to what Catalog needs.

Lifecycle and cost: the job is manual-trigger only, so it holds no replica and costs nothing
between runs. It is created immediately before use and deleted in `finally`, which also keeps
repeated runs from colliding on the same job name.

The `CommandRunner` and `SleepAction` parameters exist so scripts/tests/internal-smoke-check.ps1
can drive every branch without Azure, a network or a real job.
#>

function Invoke-AzCli {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string[]]$AzArguments,
        [scriptblock]$CommandRunner
    )

    if ($CommandRunner) {
        return & $CommandRunner -Arguments $AzArguments
    }

    # az writes progress to stderr; treating that as terminating would fail on success.
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = @(& az @AzArguments 2>&1 | ForEach-Object { $_.ToString() })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Assert-AzCliSucceeded {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [pscustomobject]$Result,
        [Parameter(Mandatory)] [string]$Operation
    )

    if ($Result.ExitCode -ne 0) {
        throw "$Operation failed: $($Result.Output -join [Environment]::NewLine)"
    }
}

function Invoke-CatalogContainerAppJob {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$ResourceGroup,

        # Resolves the environment and location, so the job always lands in the same
        # Container Apps Environment as the app under test rather than a named guess.
        [Parameter(Mandatory)] [string]$ContainerApp,

        [Parameter(Mandatory)] [string]$Image,
        [Parameter(Mandatory)] [string]$Command,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [string]$JobName = 'caj-catalog-smoke-run',
        [ValidateRange(1, 120)] [int]$MaxAttempts = 60,
        [ValidateRange(0, 60)] [int]$DelaySeconds = 5,
        [scriptblock]$CommandRunner,
        [scriptblock]$SleepAction = { param([int]$Seconds) Start-Sleep -Seconds $Seconds }
    )

    $environmentResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
        'containerapp', 'show',
        '--resource-group', $ResourceGroup,
        '--name', $ContainerApp,
        '--query', '{environmentId:properties.managedEnvironmentId,location:location}',
        '--output', 'json',
        '--only-show-errors'
    )
    Assert-AzCliSucceeded -Result $environmentResult -Operation "Resolve the environment for '$ContainerApp'"

    $environmentJson = $environmentResult.Output -join [Environment]::NewLine
    try {
        $environmentDetails = $environmentJson | ConvertFrom-Json
    }
    catch {
        throw "Container App '$ContainerApp' returned invalid environment metadata: $environmentJson"
    }

    $environmentId = [string]$environmentDetails.environmentId
    $location = [string]$environmentDetails.location
    if ([string]::IsNullOrWhiteSpace($environmentId) -or [string]::IsNullOrWhiteSpace($location)) {
        throw "Container App '$ContainerApp' did not report a managed environment."
    }

    $environmentMarker = '/providers/Microsoft.App/managedEnvironments/'
    $environmentMarkerIndex = $environmentId.IndexOf($environmentMarker, [StringComparison]::OrdinalIgnoreCase)
    if ($environmentMarkerIndex -lt 0) {
        throw "Container App '$ContainerApp' returned an invalid managed environment resource ID."
    }

    $resourceGroupScope = $environmentId.Substring(0, $environmentMarkerIndex)
    $jobUri = "$resourceGroupScope/providers/Microsoft.App/jobs/${JobName}?api-version=2025-07-01"
    $templatePath = Join-Path ([IO.Path]::GetTempPath()) "catalog-smoke-$([Guid]::NewGuid().ToString('N')).json"
    $template = [ordered]@{
        location = $location
        properties = [ordered]@{
            environmentId = $environmentId
            configuration = [ordered]@{
                replicaTimeout = 600
                replicaRetryLimit = 0
                manualTriggerConfig = [ordered]@{
                    replicaCompletionCount = 1
                    parallelism = 1
                }
                triggerType = 'Manual'
            }
            template = [ordered]@{
                containers = @(
                    [ordered]@{
                        name = 'catalog-smoke'
                        image = $Image
                        command = @($Command)
                        args = @($Arguments)
                        resources = [ordered]@{
                            cpu = 0.25
                            memory = '0.5Gi'
                        }
                    }
                )
            }
        }
    }
    [IO.File]::WriteAllText(
        $templatePath,
        ($template | ConvertTo-Json -Depth 20 -Compress),
        [Text.UTF8Encoding]::new($false)
    )

    $jobCreated = $false
    try {
        # PUT rather than create: an identical definition converges instead of colliding, so
        # a retried pipeline run is safe even if a previous run failed before cleanup.
        $createResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
            'rest',
            '--method', 'put',
            '--uri', $jobUri,
            '--body', "@$templatePath",
            '--output', 'none',
            '--only-show-errors'
        )
        Assert-AzCliSucceeded -Result $createResult -Operation "Create the manual smoke job '$JobName'"
        $jobCreated = $true

        $provisioningState = 'Unknown'
        for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
            $provisioningResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
                'containerapp', 'job', 'show',
                '--resource-group', $ResourceGroup,
                '--name', $JobName,
                '--query', 'properties.provisioningState',
                '--output', 'tsv',
                '--only-show-errors'
            )
            Assert-AzCliSucceeded -Result $provisioningResult -Operation "Read smoke job '$JobName' provisioning state"

            $provisioningState = ($provisioningResult.Output -join '').Trim()
            if ($provisioningState -eq 'Succeeded') { break }
            if ($provisioningState -in @('Failed', 'Canceled')) {
                throw "Manual smoke job '$JobName' provisioning finished with status '$provisioningState'."
            }
            if ($attempt -eq $MaxAttempts) {
                throw "Manual smoke job '$JobName' did not provision after $MaxAttempts attempts. Last status: '$provisioningState'."
            }

            # Polled, never slept blindly: Azure reports the state, so the script asks.
            & $SleepAction $DelaySeconds
        }

        $startResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
            'containerapp', 'job', 'start',
            '--resource-group', $ResourceGroup,
            '--name', $JobName,
            '--query', 'name',
            '--output', 'tsv',
            '--only-show-errors'
        )
        Assert-AzCliSucceeded -Result $startResult -Operation "Start the manual smoke job '$JobName'"

        $executionName = ($startResult.Output -join '').Trim()
        if ([string]::IsNullOrWhiteSpace($executionName)) {
            throw "The manual smoke job '$JobName' did not return an execution name."
        }

        $lastStatus = 'Unknown'
        for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
            $statusResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
                'containerapp', 'job', 'execution', 'show',
                '--resource-group', $ResourceGroup,
                '--name', $JobName,
                '--job-execution-name', $executionName,
                '--query', 'properties.status',
                '--output', 'tsv',
                '--only-show-errors'
            )
            Assert-AzCliSucceeded -Result $statusResult -Operation "Read smoke execution '$executionName'"

            $lastStatus = ($statusResult.Output -join '').Trim()
            if ($lastStatus -eq 'Succeeded') {
                return [pscustomobject]@{
                    JobName = $JobName
                    ExecutionName = $executionName
                    Status = $lastStatus
                }
            }

            if ($lastStatus -in @('Failed', 'Stopped', 'Degraded')) {
                $logsResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
                    'containerapp', 'job', 'logs', 'show',
                    '--resource-group', $ResourceGroup,
                    '--name', $JobName,
                    '--execution', $executionName,
                    '--container', 'catalog-smoke',
                    '--tail', '100',
                    '--format', 'text',
                    '--only-show-errors'
                )
                $logs = if ($logsResult.ExitCode -eq 0) {
                    $logsResult.Output -join [Environment]::NewLine
                }
                else {
                    "Logs unavailable: $($logsResult.Output -join [Environment]::NewLine)"
                }

                # Same redaction the migration script applies: diagnostics must never turn a
                # failed smoke into a credential disclosure in a public build log.
                $safeLogs = $logs `
                    -replace '(?i)(password|pwd)=[^;\s]+', '$1=[REDACTED]' `
                    -replace '(?i)(connectionstrings?__[A-Za-z0-9_]+)\s*=\s*\S+', '$1=[REDACTED]'
                throw "Catalog smoke execution '$executionName' finished with status '$lastStatus'. Logs:$([Environment]::NewLine)$safeLogs"
            }

            if ($attempt -lt $MaxAttempts) {
                & $SleepAction $DelaySeconds
            }
        }

        throw "Catalog smoke execution '$executionName' did not complete after $MaxAttempts attempts. Last status: '$lastStatus'."
    }
    finally {
        if ($jobCreated) {
            $deleteResult = Invoke-AzCli -CommandRunner $CommandRunner -AzArguments @(
                'containerapp', 'job', 'delete',
                '--resource-group', $ResourceGroup,
                '--name', $JobName,
                '--yes',
                '--only-show-errors'
            )
            if ($deleteResult.ExitCode -ne 0) {
                Write-Warning "Failed to remove the temporary smoke job '$JobName': $($deleteResult.Output -join [Environment]::NewLine)"
            }
        }
        if ([IO.File]::Exists($templatePath)) {
            [IO.File]::Delete($templatePath)
        }
    }
}
