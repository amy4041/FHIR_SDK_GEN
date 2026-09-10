#Requires -Version 7.2

[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$buildPropsPath = Join-Path $root 'Directory.Build.props'
$codeGenProjectPath = Join-Path $root 'CodeGen/MyFhirSdk.CodeGen.csproj'
$descriptorPath = Join-Path $root 'CodeGen/Policy/runtime-contract.json'
$matrixPath = Join-Path $root 'CodeGen/Compatibility/GenerationCompatibilityMatrix.cs'
$toolManifestPath = Join-Path $root '.config/dotnet-tools.json'
$globalJsonPath = Join-Path $root 'global.json'
$packageSnapshotPath = Join-Path $root 'Tests/CodeGen/Packaging/codegen-tool-package-layout.txt'
$gitAttributesPath = Join-Path $root '.gitattributes'

[xml] $buildProps = Get-Content -LiteralPath $buildPropsPath -Raw -Encoding utf8
$targetFramework = [string] $buildProps.Project.PropertyGroup.MyFhirSdkTargetFramework
if ($targetFramework -notmatch '^net(?<major>[1-9][0-9]*)\.0$') {
    throw "MyFhirSdkTargetFramework '$targetFramework' is not a supported single-target TFM."
}
$targetMajor = [int] $Matches.major

[xml] $codeGenProject = Get-Content -LiteralPath $codeGenProjectPath -Raw -Encoding utf8
$projectProperties = $codeGenProject.SelectSingleNode(
    '/Project/PropertyGroup[ExpectedToolPackageId]')
if ($null -eq $projectProperties) {
    throw 'CodeGen tool contract property group was not found.'
}
$packageId = [string] $projectProperties.ExpectedToolPackageId
$packageVersion = [string] $projectProperties.ExpectedToolPackageVersion
$toolCommand = [string] $projectProperties.ExpectedToolCommandName
if ([string] $projectProperties.ToolContractTargetFramework -cne '$(MyFhirSdkTargetFramework)') {
    throw 'CodeGen ToolContractTargetFramework must derive from MyFhirSdkTargetFramework.'
}

$descriptor = Get-Content -LiteralPath $descriptorPath -Raw -Encoding utf8 | ConvertFrom-Json
if ([string] $descriptor.targetFramework -cne $targetFramework -or
    [string] $descriptor.compilerReference.targetFramework -cne $targetFramework) {
    throw 'Runtime descriptor TFM fields do not match Directory.Build.props.'
}
if (-not ([string] $descriptor.compilerReference.logicalName).EndsWith(
        "/$targetFramework",
        [System.StringComparison]::Ordinal)) {
    throw 'Runtime compiler reference logical name does not contain the configured TFM.'
}
if ([string] $descriptor.compatibility.toolVersion -cne $packageVersion -or
    [string] $descriptor.compatibility.codeGenVersion -cne $packageVersion) {
    throw 'Runtime descriptor tool/CodeGen versions do not match the tool package version.'
}

$matrixSource = Get-Content -LiteralPath $matrixPath -Raw -Encoding utf8
function Read-MatrixConstant {
    param([string] $Name)

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $matrixSource,
        "public const string $Name = `"(?<value>[^`"]+)`";")
    if (-not $match.Success) {
        throw "GenerationCompatibilityMatrix.$Name was not found."
    }
    return $match.Groups['value'].Value
}
if ((Read-MatrixConstant 'ToolPackageId') -cne $packageId -or
    (Read-MatrixConstant 'ToolVersion') -cne $packageVersion -or
    (Read-MatrixConstant 'CodeGenVersion') -cne $packageVersion -or
    (Read-MatrixConstant 'TargetFramework') -cne $targetFramework) {
    throw 'GenerationCompatibilityMatrix does not match the centralized toolchain contract.'
}

$toolManifest = Get-Content -LiteralPath $toolManifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$toolEntries = @($toolManifest.tools.PSObject.Properties)
if ($toolEntries.Count -ne 1 -or
    -not [string]::Equals(
        $toolEntries[0].Name,
        $packageId,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    [string] $toolEntries[0].Value.version -cne $packageVersion -or
    @($toolEntries[0].Value.commands).Count -ne 1 -or
    [string] @($toolEntries[0].Value.commands)[0] -cne $toolCommand) {
    throw 'Repository tool manifest identity does not match the toolchain contract.'
}

$globalJson = Get-Content -LiteralPath $globalJsonPath -Raw -Encoding utf8 | ConvertFrom-Json
$sdkMajor = [System.Version]::Parse([string] $globalJson.sdk.version).Major
if ($sdkMajor -ne $targetMajor) {
    throw "global.json SDK major '$sdkMajor' does not match TFM major '$targetMajor'."
}

$artifactsRoot = Join-Path $root 'artifacts'
$literalProjects = @(Get-ChildItem -LiteralPath $root -Filter '*.csproj' -Recurse -File | Where-Object {
    -not $_.FullName.StartsWith(
        $artifactsRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and
    (Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8) -match '<TargetFramework>\s*net[0-9]'
})
if ($literalProjects.Count -ne 0) {
    throw 'Project-level target framework literals remain; use MyFhirSdkTargetFramework.'
}
$packageSnapshot = Get-Content -LiteralPath $packageSnapshotPath -Raw -Encoding utf8
if ($packageSnapshot.IndexOf(
        $targetFramework,
        [System.StringComparison]::Ordinal) -ge 0) {
    throw 'Package inventory snapshot must normalize target framework paths as <tfm>.'
}
$gitAttributes = Get-Content -LiteralPath $gitAttributesPath -Raw -Encoding utf8
if ($gitAttributes -notmatch '(?m)^Generated/R5/\*\* text eol=lf\s*$') {
    throw 'Generated/R5 artifacts must be pinned to LF in .gitattributes.'
}
if ($gitAttributes -notmatch '(?m)^CodeGen/\*\* text eol=lf\s*$') {
    throw 'CodeGen package inputs must be pinned to LF in .gitattributes.'
}

$inventoryScript = Get-Content -LiteralPath (
    Join-Path $root 'eng/Write-CodeGenToolPackageInventory.ps1') -Raw -Encoding utf8
if ($inventoryScript.IndexOf(
        '<platform-build-output>',
        [System.StringComparison]::Ordinal) -lt 0 -or
    $inventoryScript.IndexOf(
        'Get-NormalizedTextHash',
        [System.StringComparison]::Ordinal) -lt 0) {
    throw 'Package inventory must normalize platform-generated binary and text outputs.'
}

$result = [ordered]@{
    schemaVersion = 1
    status = 'passed'
    targetFramework = $targetFramework
    dotnetSdkVersion = [string] $globalJson.sdk.version
    packageId = $packageId
    packageVersion = $packageVersion
    toolCommand = $toolCommand
    runtimeContractVersion = [string] $descriptor.contractVersion
    compilerReferenceLogicalName = [string] $descriptor.compilerReference.logicalName
    compilerReferenceSha256 = [string] $descriptor.compilerReference.sha256
}
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    [void] [System.IO.Directory]::CreateDirectory(
        [System.IO.Path]::GetDirectoryName($fullOutputPath))
    [System.IO.File]::WriteAllText(
        $fullOutputPath,
        (($result | ConvertTo-Json -Depth 4) + "`n"),
        [System.Text.UTF8Encoding]::new($false))
}
else {
    $result | ConvertTo-Json -Depth 4
}
