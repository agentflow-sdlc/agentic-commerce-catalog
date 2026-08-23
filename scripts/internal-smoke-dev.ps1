#Requires -Version 7.0

<#
.SYNOPSIS
Read-only Catalog smoke verification executed inside the Container Apps Environment.

.DESCRIPTION
Replaces the previous smoke transport, which issued HTTP requests from a GitHub-hosted
runner against a public Catalog FQDN. Catalog ingress is internal-only, so that route no
longer exists and must not be recreated: the fix is to move the requests inside the
environment, not to reopen the service.

    GitHub-hosted runner
      -> Azure OIDC -> Azure Resource Manager
      -> manual Container Apps Job (created, started, polled, deleted)
      -> internal HTTPS -> Catalog API
      -> GET /health, GET /products, GET /categories

The checks themselves are the ones the public smoke already performed, moved verbatim in
intent: status, correlation header, JSON validity, response envelope and a guard against
leaked internals. GET only -- deployment verification never mutates Catalog data.

The execution image only needs a shell and an HTTP client. It is a Microsoft-hosted
PowerShell image from the same registry as the application base images, pulled anonymously,
so no registry credential and no new stored secret are involved.
#>

[CmdletBinding()]
param(
    # Pinned by version tag, matching how the Dockerfiles pin their bases. Overridable so a
    # tag correction does not require a code change.
    [string]$ExecutionImage = 'mcr.microsoft.com/powershell:7.5-azurelinux-3.0',
    [string]$EvidencePath,
    [ValidateRange(1, 120)] [int]$MaxAttempts = 60,
    [ValidateRange(0, 60)] [int]$DelaySeconds = 5
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'azure-containerapp-job.ps1')

$context = & (Join-Path $PSScriptRoot 'bootstrap-pulumi.ps1') -PassThru
$infrastructureDirectory = Join-Path $PSScriptRoot '..\infra\Catalog.Infrastructure'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$previousLocation = Get-Location

try {
    Set-Location -LiteralPath $infrastructureDirectory
    $resourceGroup = (& $context.PulumiPath stack output resourceGroupName).Trim()
    $catalogApiName = (& $context.PulumiPath stack output catalogApiName).Trim()
    $catalogUrl = (& $context.PulumiPath stack output catalogUrl).Trim().TrimEnd('/')

    if ([string]::IsNullOrWhiteSpace($resourceGroup) -or [string]::IsNullOrWhiteSpace($catalogApiName)) {
        throw 'The Catalog workload is not deployed; there is nothing to verify.'
    }

    if (-not $catalogUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Catalog did not expose a valid HTTPS endpoint.'
    }

    # `.internal.` is what Container Apps puts in the FQDN of an app with internal ingress.
    # Asserting it here means an accidental return to external ingress fails verification
    # loudly instead of quietly passing over a public endpoint.
    if ($catalogUrl -notmatch '(?i)\.internal\.') {
        throw "Catalog endpoint '$catalogUrl' is not an internal Container Apps FQDN. " +
            'Ingress must be internal; refusing to smoke test a publicly exposed Catalog.'
    }

    # One job name per environment, derived from the deployed app, so concurrent runs against
    # different environments never collide and a retry converges on the same name.
    $suffix = ($catalogApiName -split '-')[-1]
    $jobName = "caj-catalog-smoke-$suffix"

    $inner = @'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$catalogUrl = '__CATALOG_URL__'
$transient = @(408, 429, 500, 502, 503, 504)
$results = @()

function Invoke-CatalogGet {
    param([string]$Path)

    # The app scales to zero, so the first request after an idle period pays a cold start.
    # Retry only transient conditions; a 4xx that is not 429 is a real failure.
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$catalogUrl$Path" -Method Get `
                -SkipHttpErrorCheck -TimeoutSec 60
        }
        catch {
            if ($attempt -eq 6) { throw "GET $Path never became reachable: $($_.Exception.Message)" }
            Start-Sleep -Seconds 5
            continue
        }

        if ($response.StatusCode -eq 200) { return $response }
        if ($response.StatusCode -in $transient -and $attempt -lt 6) {
            Start-Sleep -Seconds 5
            continue
        }

        throw "GET $Path returned HTTP $($response.StatusCode)."
    }
}

foreach ($path in @('/health', '/products', '/categories')) {
    $response = Invoke-CatalogGet -Path $path

    $correlationId = [string]$response.Headers['X-Correlation-ID']
    if ([string]::IsNullOrWhiteSpace($correlationId)) {
        throw "GET $path did not return X-Correlation-ID."
    }

    try { $parsed = $response.Content | ConvertFrom-Json -NoEnumerate }
    catch { throw "GET $path did not return valid JSON." }

    if ($response.Content -match '(?i)(stack trace|connection string|password=|server=tcp:)') {
        throw "GET $path exposed internal or sensitive information."
    }

    # Collections use the documented { data, correlationId } envelope, so the array is under
    # .data rather than at the root. /health is the deliberate exception and returns its
    # payload directly.
    $isCollection = $path -in @('/products', '/categories')
    if ($isCollection) {
        if ($parsed.PSObject.Properties.Name -notcontains 'data') {
            throw "GET $path did not return a 'data' envelope property."
        }
        if (@($parsed.data) -isnot [System.Array]) {
            throw "GET $path did not return a JSON collection in 'data'."
        }
        if ([string]::IsNullOrWhiteSpace([string]$parsed.correlationId)) {
            throw "GET $path did not return a correlationId in its envelope."
        }
    }
    else {
        if ([string]$parsed.status -ne 'Healthy') {
            throw "GET $path reported status '$($parsed.status)' instead of 'Healthy'."
        }
    }

    $results += [pscustomobject]@{
        endpoint = $path
        status = $response.StatusCode
        collection = $isCollection
    }
}

Write-Output ('CATALOG_SMOKE_OK ' + (($results | ForEach-Object { "$($_.endpoint)=$($_.status)" }) -join ' '))
'@

    $inner = $inner.Replace('__CATALOG_URL__', $catalogUrl)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($inner))

    $result = Invoke-CatalogContainerAppJob `
        -ResourceGroup $resourceGroup `
        -ContainerApp $catalogApiName `
        -Image $ExecutionImage `
        -Command 'pwsh' `
        -Arguments @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded) `
        -JobName $jobName `
        -MaxAttempts $MaxAttempts `
        -DelaySeconds $DelaySeconds

    $commitSha = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
    $evidence = [pscustomobject]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        commit = $commitSha
        transport = 'container-app-job'
        job = $result.JobName
        execution = $result.ExecutionName
        status = $result.Status
        # Internal FQDN, deliberately recorded so evidence shows what was verified and that
        # it was not a public address. It is not a secret and resolves only inside the
        # Container Apps Environment.
        target = $catalogUrl
        targetContainerApp = $catalogApiName
        publicIngress = $false
        readOnly = $true
        validatedChecks = @(
            'health-endpoint-healthy'
            'products-collection-envelope'
            'categories-collection-envelope'
            'correlation-id-header'
            'valid-json'
            'no-sensitive-content'
        )
    }

    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $evidenceDirectory = Split-Path -Parent $EvidencePath
        if (-not [string]::IsNullOrWhiteSpace($evidenceDirectory)) {
            New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
        }
        $evidence | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $EvidencePath -Encoding utf8NoBOM
    }

    $evidence
}
finally {
    Set-Location -LiteralPath $previousLocation
}
