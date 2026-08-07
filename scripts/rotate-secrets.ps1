#Requires -Version 7.0

<#
.SYNOPSIS
Rotates the Pulumi passphrase and the Azure SQL administrator password.

.DESCRIPTION
Both credentials were exposed in plaintext and have to be replaced. The encrypted SQL
password lives in Pulumi.dev.yaml, which is committed to a PUBLIC repository, so anyone
holding the leaked passphrase can decrypt it. Rotating only one of the two is pointless:
a new SQL password re-encrypted under the leaked passphrase is just as readable, and a new
passphrase protecting the already-leaked password protects nothing.

No secret is ever printed, written to a log, or passed as a command-line argument. The
current passphrase is read from the environment so it never appears in shell history.

Run interactively from the repository root:

    $env:PULUMI_CONFIG_PASSPHRASE = '<current passphrase>'
    pwsh ./scripts/rotate-secrets.ps1
    $env:PULUMI_CONFIG_PASSPHRASE = $null

This script deliberately does NOT run `pulumi up`. It only rewrites the stack config, so
the change stays reviewable in a commit. The deployment pipeline applies the new password
to Azure SQL and refreshes the Key Vault connection string in a single run.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Repository = 'agentflow-sdlc/agentic-commerce-catalog',

    # Skips updating the GitHub secrets, for a dry run against the local config only.
    [switch]$SkipGitHubSecrets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$oldPassphrase = $env:PULUMI_CONFIG_PASSPHRASE
if ([string]::IsNullOrWhiteSpace($oldPassphrase)) {
    throw 'Set PULUMI_CONFIG_PASSPHRASE to the CURRENT passphrase before running this script.'
}

function New-RandomSecret {
    param(
        [Parameter(Mandatory)][int]$Length,
        [Parameter(Mandatory)][string]$Alphabet
    )

    # RandomNumberGenerator rather than Get-Random: these values guard a public repository
    # and a reachable database, so the generator has to be cryptographic.
    $bytes = [byte[]]::new($Length * 4)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

    $builder = [System.Text.StringBuilder]::new($Length)
    for ($i = 0; $i -lt $Length; $i++) {
        $value = [System.BitConverter]::ToUInt32($bytes, $i * 4)
        [void]$builder.Append($Alphabet[$value % $Alphabet.Length])
    }

    return $builder.ToString()
}

# ';', '=', '"' and '\'' terminate or escape fields inside an ADO.NET connection string, and
# the password is interpolated into one. Excluding them avoids a password that is valid in
# Azure but unusable by the application.
$sqlAlphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#%*+-.:?_~'
$passphraseAlphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'

$newSqlPassword = New-RandomSecret -Length 40 -Alphabet $sqlAlphabet
$newPassphrase = New-RandomSecret -Length 48 -Alphabet $passphraseAlphabet

$context = & (Join-Path $PSScriptRoot 'bootstrap-pulumi.ps1') -PassThru
$pulumi = $context.PulumiPath
$infrastructureDirectory = Join-Path $PSScriptRoot '..\infra\Catalog.Infrastructure'
$previousLocation = Get-Location

try {
    Set-Location -LiteralPath $infrastructureDirectory

    if (-not $PSCmdlet.ShouldProcess('stack dev', 'rotate SQL password and Pulumi passphrase')) {
        return
    }

    Write-Host 'Step 1/4  Setting the new SQL administrator password.'
    & $pulumi config set --secret catalog:sqlAdminPassword $newSqlPassword --non-interactive | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to set the new SQL password in the stack config.' }

    Write-Host 'Step 2/4  Re-encrypting the stack under a new passphrase.'
    # change-secrets-provider has no flag for the new passphrase: it prompts, and asks twice
    # for confirmation. The old passphrase still comes from the environment, for decryption.
    $answers = "$newPassphrase`n$newPassphrase`n"
    $answers | & $pulumi stack change-secrets-provider passphrase | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to change the secrets provider passphrase.' }

    Write-Host 'Step 3/4  Verifying the rotation actually took effect.'
    # The CLI prompt behaviour is the fragile part of this script, so trust nothing and
    # verify both directions: the new passphrase must decrypt, the old one must not.
    $env:PULUMI_CONFIG_PASSPHRASE = $newPassphrase
    $decrypted = & $pulumi config get catalog:sqlAdminPassword 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'The new passphrase does not decrypt the stack. Rotation failed; do not commit.' }
    if ($decrypted -ne $newSqlPassword) { throw 'The stack does not hold the new SQL password. Rotation failed; do not commit.' }

    $env:PULUMI_CONFIG_PASSPHRASE = $oldPassphrase
    & $pulumi config get catalog:sqlAdminPassword *>$null
    if ($LASTEXITCODE -eq 0) { throw 'The OLD passphrase still decrypts the stack. The passphrase was not rotated; do not commit.' }
    $global:LASTEXITCODE = 0

    $env:PULUMI_CONFIG_PASSPHRASE = $newPassphrase
    Write-Host '          Verified: new passphrase decrypts, old passphrase rejected.'

    if ($SkipGitHubSecrets) {
        Write-Host 'Step 4/4  Skipped (-SkipGitHubSecrets). GitHub still holds the OLD values.'
    }
    else {
        Write-Host 'Step 4/4  Updating the GitHub Actions secrets.'
        $newPassphrase | gh secret set PULUMI_CONFIG_PASSPHRASE --repo $Repository
        if ($LASTEXITCODE -ne 0) { throw 'Failed to update PULUMI_CONFIG_PASSPHRASE in GitHub.' }

        $newSqlPassword | gh secret set CATALOG_SQL_ADMIN_PASSWORD --repo $Repository
        if ($LASTEXITCODE -ne 0) { throw 'Failed to update CATALOG_SQL_ADMIN_PASSWORD in GitHub.' }
    }

    Write-Host ''
    Write-Host 'Rotation complete. Neither secret was printed.'
    Write-Host 'Next: commit the re-encrypted infra/Catalog.Infrastructure/Pulumi.dev.yaml,'
    Write-Host 'then let the pipeline deploy so Azure SQL and Key Vault pick up the new password.'
}
finally {
    Set-Location -LiteralPath $previousLocation
    $env:PULUMI_CONFIG_PASSPHRASE = $null
}
