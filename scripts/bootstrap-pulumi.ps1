#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$PassThru
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-RequiredCommand {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    if ($Name -eq 'pulumi') {
        $candidate = 'C:\Program Files (x86)\Pulumi\pulumi.exe'
        if (Test-Path -LiteralPath $candidate) {
            $env:PATH = "$(Split-Path -Parent $candidate);$env:PATH"
            return $candidate
        }
    }

    throw "Required command '$Name' is not installed or is not available in PATH."
}

function Set-PulumiSecretFromStandardInput {
    param(
        [Parameter(Mandatory)][string]$PulumiPath,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Value
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $PulumiPath
    $startInfo.WorkingDirectory = (Get-Location).Path
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('config')
    $startInfo.ArgumentList.Add('set')
    $startInfo.ArgumentList.Add($Key)
    $startInfo.ArgumentList.Add('--secret')
    $startInfo.ArgumentList.Add('--non-interactive')

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $null = $process.Start()
    $process.StandardInput.WriteLine($Value)
    $process.StandardInput.Close()
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        $safeError = "$standardError $standardOutput".Replace($Value, '[REDACTED]')
        throw "Pulumi could not configure secret '$Key'. $safeError"
    }
}

$azPath = Resolve-RequiredCommand -Name 'az'
$pulumiPath = Resolve-RequiredCommand -Name 'pulumi'

$account = & $azPath account show --output json | ConvertFrom-Json
if ($account.state -ne 'Enabled') {
    throw 'The active Azure subscription is not enabled.'
}

$stateResourceGroup = 'rg-agentic-pulumi-state'
$resourceGroupExists = & $azPath group exists --name $stateResourceGroup
if ($resourceGroupExists -ne 'true') {
    throw "The required Pulumi state resource group '$stateResourceGroup' does not exist."
}

$storageAccounts = @(
    & $azPath storage account list `
        --resource-group $stateResourceGroup `
        --query '[].{name:name,location:location}' `
        --output json | ConvertFrom-Json
)

$matchingAccounts = @()
foreach ($storageAccount in $storageAccounts) {
    $containerExists = & $azPath storage container exists `
        --account-name $storageAccount.name `
        --name 'pulumi-state' `
        --auth-mode login `
        --query exists `
        --output tsv
    if ($containerExists -eq 'true') {
        $matchingAccounts += $storageAccount
    }
}

if ($matchingAccounts.Count -ne 1) {
    throw "Expected exactly one Storage Account with container 'pulumi-state' in '$stateResourceGroup'; found $($matchingAccounts.Count)."
}

$storage = $matchingAccounts[0]
if ($storage.name -notmatch '(?<suffix>\d{6})$') {
    throw 'The existing Pulumi Storage Account does not end with the required deterministic six-digit suffix.'
}

$suffix = $Matches.suffix
$env:AZURE_STORAGE_ACCOUNT = $storage.name
$backendUrl = "azblob://pulumi-state?storage_account=$($storage.name)"
$infrastructureDirectory = Join-Path $PSScriptRoot '..\infra\Catalog.Infrastructure'
$previousLocation = Get-Location

try {
    Set-Location -LiteralPath $infrastructureDirectory

    & $pulumiPath login $backendUrl --non-interactive | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Pulumi could not log in to the existing Azure Blob backend.'
    }

    & $pulumiPath whoami --verbose | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Pulumi backend identity validation failed.'
    }

    if ([string]::IsNullOrWhiteSpace($env:PULUMI_CONFIG_PASSPHRASE)) {
        throw 'PULUMI_CONFIG_PASSPHRASE must be present in the process environment.'
    }

    # Azure Pipelines leaves "$(NAME)" untouched when the variable is not defined, which
    # would otherwise surface much later as an unexplained decryption failure.
    if ($env:PULUMI_CONFIG_PASSPHRASE -match '^\$\(.+\)$') {
        throw 'PULUMI_CONFIG_PASSPHRASE was not substituted. Define it as a secret variable in the catalog-dev variable group.'
    }

    $stacks = @(& $pulumiPath stack ls --json | ConvertFrom-Json)
    $devStack = $stacks | Where-Object { $_.name -eq 'dev' -or $_.name -like '*/dev' }
    if ($null -eq $devStack) {
        & $pulumiPath stack init dev --secrets-provider passphrase --non-interactive | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Pulumi stack 'dev' could not be initialized."
        }
    }
    else {
        & $pulumiPath stack select dev --non-interactive | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Pulumi stack 'dev' could not be selected."
        }
    }

    $stackExport = & $pulumiPath stack export | ConvertFrom-Json -Depth 100
    $secretsProviderType = $stackExport.deployment.secrets_providers.type
    if ($secretsProviderType -ne 'passphrase') {
        throw "Stack 'dev' uses secrets provider '$secretsProviderType'; automatic migration is prohibited."
    }

    & $pulumiPath config set azure-native:location $storage.location --non-interactive | Out-Host
    & $pulumiPath config set catalog:nameSuffix $suffix --non-interactive | Out-Host
    & $pulumiPath config set catalog:sqlAdminLogin catalogsqladmin --non-interactive | Out-Host
    & $pulumiPath config set catalog:sqlDatabaseSku Basic --non-interactive | Out-Host
    $sqlLocation = & $pulumiPath config get catalog:sqlLocation 2>$null
    if ([string]::IsNullOrWhiteSpace($sqlLocation) `
        -or $sqlLocation -in @('eastus', 'eastus2')) {
        & $pulumiPath config set catalog:sqlLocation centralus --non-interactive | Out-Host
    }

    $configText = (& $pulumiPath config) -join "`n"
    $sqlPasswordConfigured = $configText -match '(?m)^catalog:sqlAdminPassword\s+\[secret\]\s*$'
    if (-not [string]::IsNullOrWhiteSpace($env:CATALOG_SQL_ADMIN_PASSWORD)) {
        Set-PulumiSecretFromStandardInput `
            -PulumiPath $pulumiPath `
            -Key 'catalog:sqlAdminPassword' `
            -Value $env:CATALOG_SQL_ADMIN_PASSWORD
        $sqlPasswordConfigured = $true
    }

    if (-not $sqlPasswordConfigured) {
        throw 'catalog:sqlAdminPassword is not configured and CATALOG_SQL_ADMIN_PASSWORD is unavailable.'
    }

    $stackSettingsPath = Join-Path $infrastructureDirectory 'Pulumi.dev.yaml'
    $stackSettings = Get-Content -Raw -LiteralPath $stackSettingsPath
    if ($stackSettings -notmatch '(?m)^encryptionsalt:' `
        -or $stackSettings -notmatch '(?ms)catalog:sqlAdminPassword:\s*\r?\n\s+secure:') {
        throw 'Pulumi.dev.yaml does not contain the expected passphrase encryption metadata and encrypted SQL secret.'
    }

    Write-Host "Pulumi backend and stack 'dev' are ready in $($storage.location)."

    if ($PassThru) {
        [pscustomobject]@{
            SubscriptionName = $account.name
            Location = $storage.location
            StorageAccount = $storage.name
            BackendUrl = $backendUrl
            Stack = 'dev'
            NameSuffix = $suffix
            PulumiPath = $pulumiPath
        }
    }
}
finally {
    Set-Location -LiteralPath $previousLocation
}
