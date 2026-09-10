#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $CurrentPackagePath,
    [Parameter(Mandatory)] [string] $BaselinePath,
    [Parameter(Mandatory)] [string] $OutputPath,
    [string] $PreviousPackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SemanticVersion.ps1')

function Get-PackageIdentity {
    param([string] $Path)

    $package = (Resolve-Path -LiteralPath $Path).Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.Xml.Linq
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package)
    try {
        $nuspecEntry = @($archive.Entries | Where-Object {
            $_.FullName.EndsWith('.nuspec', [System.StringComparison]::Ordinal)
        })
        if ($nuspecEntry.Count -ne 1) {
            throw "Package must contain exactly one nuspec: $Path"
        }
        $stream = $nuspecEntry[0].Open()
        try {
            $document = [System.Xml.Linq.XDocument]::Load($stream)
        }
        finally {
            $stream.Dispose()
        }
        $namespace = $document.Root.Name.Namespace
        $metadata = $document.Root.Element($namespace + 'metadata')
        return [pscustomobject]@{
            Id = [string] $metadata.Element($namespace + 'id').Value
            Version = [string] $metadata.Element($namespace + 'version').Value
        }
    }
    finally {
        $archive.Dispose()
    }
}

$baseline = Get-Content -LiteralPath (
    (Resolve-Path -LiteralPath $BaselinePath).Path) -Raw -Encoding utf8 | ConvertFrom-Json
if ([int] $baseline.schemaVersion -ne 1) {
    throw "Unsupported upgrade baseline schema '$($baseline.schemaVersion)'."
}
if ([string] $baseline.status -cne 'initial-package-baseline') {
    throw "Upgrade baseline status must be 'initial-package-baseline'."
}
if ($baseline.requirePreviousPackageWhenCurrentVersionAdvances -isnot [bool] -or
    -not [bool] $baseline.requirePreviousPackageWhenCurrentVersionAdvances) {
    throw 'Upgrade baseline must require a real previous package when the current version advances.'
}
if ([string] $baseline.sourceRevision -notmatch '^[0-9a-f]{40}$') {
    throw 'Upgrade baseline sourceRevision must be a full lowercase Git commit SHA.'
}

$current = Get-PackageIdentity $CurrentPackagePath
if (-not [string]::Equals(
        $current.Id,
        [string] $baseline.packageId,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Current package id '$($current.Id)' does not match baseline '$($baseline.packageId)'."
}
$baselineVersion = [string] $baseline.baselineVersion
$currentVersion = [string] $current.Version
$versionComparison = Compare-MyFhirSdkSemanticVersion $currentVersion $baselineVersion
if ($versionComparison -lt 0) {
    throw "Current version '$currentVersion' is older than upgrade baseline '$baselineVersion'."
}

$status = if ($versionComparison -eq 0) {
    if (-not [string]::IsNullOrWhiteSpace($PreviousPackagePath)) {
        throw 'A previous package must not be supplied while current and baseline versions are equal.'
    }
    [ordered]@{
        schemaVersion = 1
        status = 'baseline-established'
        upgradeRequired = $false
        inputsValidated = $true
        lifecycleExecutedByThisCheck = $false
        packageId = $current.Id
        baselineVersion = $baselineVersion
        currentVersion = $currentVersion
        reason = 'No distinct released tool version exists; reinstall smoke is used and is not reported as an upgrade.'
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($PreviousPackagePath)) {
        throw "Tool version advanced to '$currentVersion'; the real '$baselineVersion' package is required for upgrade smoke."
    }
    $previous = Get-PackageIdentity $PreviousPackagePath
    if (-not [string]::Equals(
            $previous.Id,
            $current.Id,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        (Compare-MyFhirSdkSemanticVersion $previous.Version $baselineVersion) -ne 0) {
        throw 'Previous package identity/version does not match the recorded upgrade baseline.'
    }
    [ordered]@{
        schemaVersion = 1
        status = 'upgrade-inputs-validated'
        upgradeRequired = $true
        inputsValidated = $true
        lifecycleExecutedByThisCheck = $false
        packageId = $current.Id
        baselineVersion = $baselineVersion
        currentVersion = $currentVersion
        reason = 'Distinct real packages are available; pass both packages to the lifecycle smoke before release.'
    }
}

$parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputPath))
[void] [System.IO.Directory]::CreateDirectory($parent)
[System.IO.File]::WriteAllText(
    $OutputPath,
    (($status | ConvertTo-Json -Depth 4) + "`n"),
    [System.Text.UTF8Encoding]::new($false))
