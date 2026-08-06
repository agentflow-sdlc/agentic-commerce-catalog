#Requires -Version 7.0

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$context = & (Join-Path $PSScriptRoot 'bootstrap-pulumi.ps1') -PassThru
$infrastructureDirectory = Join-Path $PSScriptRoot '..\infra\Catalog.Infrastructure'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$previousLocation = Get-Location

try {
    Set-Location -LiteralPath $infrastructureDirectory
    $catalogUrl = (& $context.PulumiPath stack output catalogUrl).TrimEnd('/')
    if (-not $catalogUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Catalog did not expose a valid HTTPS URL.'
    }

    $commitSha = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
    $evidenceDirectory = Join-Path $repositoryRoot "artifacts\deployment\dev\$commitSha"
    New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null

    $results = @()
    foreach ($path in @('/health', '/products', '/categories')) {
        $response = Invoke-WebRequest `
            -Uri "$catalogUrl$path" `
            -Method Get `
            -SkipHttpErrorCheck `
            -TimeoutSec 60

        if ($response.StatusCode -ne 200) {
            throw "Smoke test '$path' returned HTTP $($response.StatusCode)."
        }

        $correlationId = [string]$response.Headers['X-Correlation-ID']
        if ([string]::IsNullOrWhiteSpace($correlationId)) {
            throw "Smoke test '$path' did not return X-Correlation-ID."
        }

        try {
            $parsed = $response.Content | ConvertFrom-Json -NoEnumerate
        }
        catch {
            throw "Smoke test '$path' did not return valid JSON."
        }

        if ($response.Content -match '(?i)(stack trace|connection string|password=|server=tcp:)') {
            throw "Smoke test '$path' exposed internal or sensitive information."
        }

        $isCollection = $path -in @('/products', '/categories')
        if ($isCollection -and $parsed -isnot [System.Array]) {
            throw "Smoke test '$path' did not return a JSON collection."
        }

        $results += [pscustomobject]@{
            endpoint = $path
            status = $response.StatusCode
            contentType = [string]$response.Headers['Content-Type']
            correlationIdPresent = $true
            validJson = $true
            collection = $isCollection
        }
    }

    $evidence = [pscustomobject]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        commit = $commitSha
        baseUrl = $catalogUrl
        results = $results
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content `
        -LiteralPath (Join-Path $evidenceDirectory 'smoke-tests.json') `
        -Encoding utf8NoBOM

    $evidence
}
finally {
    Set-Location -LiteralPath $previousLocation
}
