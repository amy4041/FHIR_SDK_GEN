#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [string] $ToolManifestPath,
    [Parameter(Mandatory)] [string] $FhirPackagePath,
    [Parameter(Mandatory)] [string] $PrimitiveDefinitionsPath,
    [Parameter(Mandatory)] [string] $PrimitivePolicyPath,
    [Parameter(Mandatory)] [string] $CommittedGeneratedRoot,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [string] $PreviousPackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SemanticVersion.ps1')

function Resolve-RequiredPath {
    param([string] $Path, [string] $Name, [switch] $Directory)

    $pathType = if ($Directory) { 'Container' } else { 'Leaf' }
    if (-not (Test-Path -LiteralPath $Path -PathType $pathType)) {
        throw "$Name does not exist: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Write-Utf8NoBomLf {
    param([string] $Path, [string] $Content)

    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    [System.IO.File]::WriteAllText(
        $Path,
        $normalized,
        [System.Text.UTF8Encoding]::new($false))
}

function Invoke-DotNet {
    param(
        [string] $WorkingDirectory,
        [hashtable] $Environment,
        [string] $LogPath,
        [string[]] $Arguments
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in $Arguments) {
        [void] $startInfo.ArgumentList.Add($argument)
    }
    foreach ($entry in $Environment.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = [string] $entry.Value
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void] $process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(600000)) {
        $process.Kill($true)
        throw "dotnet command timed out: dotnet $($Arguments -join ' ')"
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode
    $process.Dispose()

    $record = @(
        "> dotnet $($Arguments -join ' ')"
        $stdout.TrimEnd()
        $stderr.TrimEnd()
        "exitCode=$exitCode"
        ''
    ) -join "`n"
    [System.IO.File]::AppendAllText(
        $LogPath,
        $record,
        [System.Text.UTF8Encoding]::new($false))

    if ($exitCode -ne 0) {
        throw "dotnet command failed with exit code $exitCode. See $LogPath"
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        StandardOutput = $stdout
        StandardError = $stderr
    }
}

function Get-FileHashMap {
    param([string] $Root, [switch] $ExcludePrimitives)

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $map = [System.Collections.Generic.SortedDictionary[string,string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($file in [System.IO.Directory]::EnumerateFiles(
            $rootPath,
            '*',
            [System.IO.SearchOption]::AllDirectories)) {
        $relative = [System.IO.Path]::GetRelativePath($rootPath, $file).Replace('\', '/')
        if ($ExcludePrimitives -and $relative.StartsWith('Primitives/', [System.StringComparison]::Ordinal)) {
            continue
        }
        $map.Add(
            $relative,
            (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant())
    }
    return ,$map
}

function Assert-HashMapsEqual {
    param(
        [System.Collections.Generic.SortedDictionary[string,string]] $Expected,
        [System.Collections.Generic.SortedDictionary[string,string]] $Actual,
        [string] $Label
    )

    if ($Expected.Count -ne $Actual.Count) {
        throw "$Label file count mismatch: actual=$($Actual.Count), expected=$($Expected.Count)."
    }
    foreach ($entry in $Expected.GetEnumerator()) {
        if (-not $Actual.ContainsKey($entry.Key)) {
            throw "$Label is missing '$($entry.Key)'."
        }
        if ($Actual[$entry.Key] -cne $entry.Value) {
            throw "$Label hash mismatch for '$($entry.Key)': actual=$($Actual[$entry.Key]), expected=$($entry.Value)."
        }
    }
}

function Get-PackageIdentity {
    param([string] $Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.Xml.Linq
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
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

function Get-PackageGenerationContract {
    param([string] $Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $descriptorEntries = @($archive.Entries | Where-Object {
            $_.FullName.EndsWith(
                '/Contracts/runtime-contract.json',
                [System.StringComparison]::Ordinal)
        })
        if ($descriptorEntries.Count -ne 1) {
            throw "Package must contain exactly one Runtime contract descriptor: $Path"
        }
        $reader = [System.IO.StreamReader]::new(
            $descriptorEntries[0].Open(),
            [System.Text.UTF8Encoding]::new($false, $true))
        try {
            $descriptor = $reader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
        }

        $policyPath = $descriptorEntries[0].FullName.Replace(
            'Contracts/runtime-contract.json',
            'Policy/primitive-generation-policy.json',
            [System.StringComparison]::Ordinal)
        $policyEntry = $archive.GetEntry($policyPath)
        if ($null -eq $policyEntry) {
            throw "Package is missing its primitive policy: $policyPath"
        }
        $policyReader = [System.IO.StreamReader]::new(
            $policyEntry.Open(),
            [System.Text.UTF8Encoding]::new($false, $true))
        try {
            $primitivePolicyContent = $policyReader.ReadToEnd()
        }
        finally {
            $policyReader.Dispose()
        }

        # Package/tool versions and compiler binary hashes can change without changing
        # generated source. The fingerprint covers the semantic generation contract.
        $fingerprintContract = [ordered]@{
            schemaVersion = $descriptor.schemaVersion
            contractVersion = $descriptor.contractVersion
            targetFramework = $descriptor.targetFramework
            runtimeAssembly = $descriptor.runtimeAssembly
            fhirPackage = $descriptor.compatibility.fhirPackage
            primitivePolicy = $descriptor.compatibility.primitivePolicy
            modelPolicies = $descriptor.compatibility.modelPolicies
            symbols = $descriptor.symbols
        }
        $fingerprintBytes = [System.Text.Encoding]::UTF8.GetBytes(
            ($fingerprintContract | ConvertTo-Json -Depth 100 -Compress))
        $fingerprint = [System.Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($fingerprintBytes)).ToLowerInvariant()
        return [pscustomobject]@{
            Fingerprint = $fingerprint
            PrimitivePolicyContent = $primitivePolicyContent
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Set-ToolManifestVersion {
    param([string] $Path, [string] $PackageId, [string] $Version)

    $document = Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json
    $property = @($document.tools.PSObject.Properties | Where-Object {
        [string]::Equals(
            $_.Name,
            $PackageId,
            [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($property.Count -ne 1) {
        throw "Tool manifest does not contain '$PackageId'."
    }
    $property[0].Value.version = $Version
    Write-Utf8NoBomLf $Path (($document | ConvertTo-Json -Depth 8) + "`n")
}

$package = Resolve-RequiredPath $PackagePath 'Tool package'
$toolManifest = Resolve-RequiredPath $ToolManifestPath 'Tool manifest'
$fhirPackage = Resolve-RequiredPath $FhirPackagePath 'FHIR package'
$primitiveDefinitions = Resolve-RequiredPath $PrimitiveDefinitionsPath 'Primitive definitions' -Directory
$primitivePolicy = Resolve-RequiredPath $PrimitivePolicyPath 'Primitive policy'
$committedGenerated = Resolve-RequiredPath $CommittedGeneratedRoot 'Committed generated root' -Directory
$previousPackage = if ([string]::IsNullOrWhiteSpace($PreviousPackagePath)) {
    $null
}
else {
    Resolve-RequiredPath $PreviousPackagePath 'Previous tool package'
}
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {
    throw "Smoke output directory must not already exist: $output"
}
[void] (New-Item -ItemType Directory -Path $output)
$logPath = Join-Path $output 'smoke.log'
Write-Utf8NoBomLf $logPath ''

$manifestDocument = Get-Content -LiteralPath $toolManifest -Raw -Encoding utf8 | ConvertFrom-Json
$toolProperty = @($manifestDocument.tools.PSObject.Properties)
if ($toolProperty.Count -ne 1) {
    throw 'The tool manifest must contain exactly one tool.'
}
$packageId = $toolProperty[0].Name
$toolVersion = [string] $toolProperty[0].Value.version
$commands = @($toolProperty[0].Value.commands)
if ($commands.Count -ne 1) {
    throw 'The tool manifest must contain exactly one command.'
}
$toolCommand = [string] $commands[0]
$currentIdentity = Get-PackageIdentity $package
if (-not [string]::Equals(
        $currentIdentity.Id,
        $packageId,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    $currentIdentity.Version -cne $toolVersion) {
    throw 'Current package identity/version does not match the repository tool manifest.'
}
# NuGet manifest keys are conventionally lower-case. Keep the nuspec casing as the
# canonical identity used by CLI/provenance assertions and output evidence.
$packageId = $currentIdentity.Id
$previousIdentity = if ($null -eq $previousPackage) { $null } else { Get-PackageIdentity $previousPackage }
$currentGenerationContract = Get-PackageGenerationContract $package
$previousGenerationContract = if ($null -eq $previousPackage) {
    $null
}
else {
    Get-PackageGenerationContract $previousPackage
}
if ($null -ne $previousIdentity) {
    if (-not [string]::Equals(
            $previousIdentity.Id,
            $packageId,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Previous and current tool package ids must match.'
    }
    if ((Compare-MyFhirSdkSemanticVersion $previousIdentity.Version $toolVersion) -ge 0) {
        throw 'Previous tool package version must be strictly older than the current version.'
    }
}

$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'MyFhirSdk-D7-Smoke-' + [System.Guid]::NewGuid().ToString('N'))
[void] (New-Item -ItemType Directory -Path $workRoot)
try {
    $manifestRoot = Join-Path $workRoot '.config'
    [void] (New-Item -ItemType Directory -Path $manifestRoot)
    $installedManifest = Join-Path $manifestRoot 'dotnet-tools.json'
    Copy-Item -LiteralPath $toolManifest -Destination $installedManifest

    $localPackageSource = Join-Path $workRoot 'package-source'
    [void] (New-Item -ItemType Directory -Path $localPackageSource)
    Copy-Item -LiteralPath $package -Destination $localPackageSource
    if ($null -ne $previousPackage) {
        Copy-Item -LiteralPath $previousPackage -Destination $localPackageSource
        Set-ToolManifestVersion $installedManifest $packageId $previousIdentity.Version
    }

    $packageSource = [System.Security.SecurityElement]::Escape(
        $localPackageSource)
    $nugetConfig = Join-Path $workRoot 'NuGet.Config'
    Write-Utf8NoBomLf $nugetConfig @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="d7-local" value="$packageSource" />
  </packageSources>
</configuration>
"@

    $environment = @{
        DOTNET_CLI_HOME = Join-Path $workRoot 'dotnet-home'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        NUGET_PACKAGES = Join-Path $workRoot 'nuget-packages'
    }

    $restoreArguments = @('tool', 'restore', '--configfile', $nugetConfig)
    [void] (Invoke-DotNet $workRoot $environment $logPath $restoreArguments)
    $help = Invoke-DotNet $workRoot $environment $logPath @($toolCommand, '--help')
    $installedVersion = if ($null -eq $previousIdentity) { $toolVersion } else { $previousIdentity.Version }
    if (-not $help.StandardOutput.Contains(
            "$packageId $installedVersion",
            [System.StringComparison]::Ordinal)) {
        throw 'Tool help does not report the manifest package identity/version.'
    }

    $modelBeforeUpgrade = $null
    $primitiveBeforeUpgrade = $null
    if ($null -ne $previousIdentity) {
        $modelBeforeUpgrade = Join-Path $workRoot 'model-before-upgrade'
        $primitiveBeforeUpgrade = Join-Path $workRoot 'primitive-before-upgrade'
        $previousPrimitivePolicy = Join-Path $workRoot 'previous-primitive-generation-policy.json'
        Write-Utf8NoBomLf `
            $previousPrimitivePolicy `
            $previousGenerationContract.PrimitivePolicyContent
        [void] (Invoke-DotNet $workRoot $environment $logPath @(
            $toolCommand,
            '--mode', 'model',
            '--input', $fhirPackage,
            '--output', $modelBeforeUpgrade,
            '--fhir-version', '5.0.0',
            '--package-id', 'hl7.fhir.r5.core',
            '--package-version', '5.0.0'))
        [void] (Invoke-DotNet $workRoot $environment $logPath @(
            $toolCommand,
            '--mode', 'primitive',
            '--input', $primitiveDefinitions,
            '--policy', $previousPrimitivePolicy,
            '--output', $primitiveBeforeUpgrade,
            '--fhir-version', '5.0.0',
            '--package-id', 'hl7.fhir.r5.core',
            '--package-version', '5.0.0'))

        Copy-Item -LiteralPath $toolManifest -Destination $installedManifest -Force
        [void] (Invoke-DotNet $workRoot $environment $logPath $restoreArguments)
        $currentHelp = Invoke-DotNet $workRoot $environment $logPath @($toolCommand, '--help')
        if (-not $currentHelp.StandardOutput.Contains(
                "$packageId $toolVersion",
                [System.StringComparison]::Ordinal)) {
            throw 'Tool help did not change to the current version after restore.'
        }
    }

    $modelFirst = Join-Path $workRoot 'model-first'
    $primitiveFirst = Join-Path $workRoot 'primitive-first'
    [void] (Invoke-DotNet $workRoot $environment $logPath @(
        $toolCommand,
        '--mode', 'model',
        '--input', $fhirPackage,
        '--output', $modelFirst,
        '--fhir-version', '5.0.0',
        '--package-id', 'hl7.fhir.r5.core',
        '--package-version', '5.0.0'))
    [void] (Invoke-DotNet $workRoot $environment $logPath @(
        $toolCommand,
        '--mode', 'primitive',
        '--input', $primitiveDefinitions,
        '--policy', $primitivePolicy,
        '--output', $primitiveFirst,
        '--fhir-version', '5.0.0',
        '--package-id', 'hl7.fhir.r5.core',
        '--package-version', '5.0.0'))

    [void] (Invoke-DotNet $workRoot $environment $logPath @(
        'tool', 'uninstall', $packageId))
    Copy-Item -LiteralPath $toolManifest -Destination $installedManifest -Force
    [void] (Invoke-DotNet $workRoot $environment $logPath $restoreArguments)

    $modelSecond = Join-Path $workRoot 'model-second'
    $primitiveSecond = Join-Path $workRoot 'primitive-second'
    [void] (Invoke-DotNet $workRoot $environment $logPath @(
        $toolCommand,
        '--mode', 'model',
        '--input', $fhirPackage,
        '--output', $modelSecond,
        '--fhir-version', '5.0.0',
        '--package-id', 'hl7.fhir.r5.core',
        '--package-version', '5.0.0'))
    [void] (Invoke-DotNet $workRoot $environment $logPath @(
        $toolCommand,
        '--mode', 'primitive',
        '--input', $primitiveDefinitions,
        '--policy', $primitivePolicy,
        '--output', $primitiveSecond,
        '--fhir-version', '5.0.0',
        '--package-id', 'hl7.fhir.r5.core',
        '--package-version', '5.0.0'))

    $committedModel = Get-FileHashMap $committedGenerated -ExcludePrimitives
    $committedPrimitives = Get-FileHashMap (Join-Path $committedGenerated 'Primitives')
    $firstModelRoot = Join-Path $modelFirst 'Generated/R5'
    $secondModelRoot = Join-Path $modelSecond 'Generated/R5'
    $firstModel = Get-FileHashMap $firstModelRoot
    $secondModel = Get-FileHashMap $secondModelRoot
    $firstPrimitives = Get-FileHashMap $primitiveFirst
    $secondPrimitives = Get-FileHashMap $primitiveSecond

    Assert-HashMapsEqual $committedModel $firstModel 'First full model generation'
    Assert-HashMapsEqual $committedModel $secondModel 'Second full model generation'
    Assert-HashMapsEqual $firstModel $secondModel 'Full model reinstall determinism'
    Assert-HashMapsEqual $committedPrimitives $firstPrimitives 'First primitive generation'
    Assert-HashMapsEqual $committedPrimitives $secondPrimitives 'Second primitive generation'
    Assert-HashMapsEqual $firstPrimitives $secondPrimitives 'Primitive reinstall determinism'

    if ($null -ne $previousIdentity) {
        $previousManifest = Get-Content -LiteralPath (
            Join-Path $modelBeforeUpgrade 'Generated/R5/model-generation-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
        $previousPrimitiveManifest = Get-Content -LiteralPath (
            Join-Path $primitiveBeforeUpgrade 'primitive-generation-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if ([string] $previousManifest.compatibility.tool.version -cne $previousIdentity.Version -or
            [string] $previousPrimitiveManifest.compatibility.tool.version -cne $previousIdentity.Version) {
            throw 'Pre-upgrade manifest provenance does not report the previous tool version in both modes.'
        }

        if ($previousGenerationContract.Fingerprint -ceq $currentGenerationContract.Fingerprint) {
            $previousModel = Get-FileHashMap (Join-Path $modelBeforeUpgrade 'Generated/R5')
            $previousPrimitives = Get-FileHashMap $primitiveBeforeUpgrade
            [void] $previousModel.Remove('model-generation-manifest.json')
            [void] $previousPrimitives.Remove('primitive-generation-manifest.json')
            $currentModelSources = Get-FileHashMap $firstModelRoot
            $currentPrimitiveSources = Get-FileHashMap $primitiveFirst
            [void] $currentModelSources.Remove('model-generation-manifest.json')
            [void] $currentPrimitiveSources.Remove('primitive-generation-manifest.json')
            Assert-HashMapsEqual $previousModel $currentModelSources 'Model source upgrade compatibility'
            Assert-HashMapsEqual $previousPrimitives $currentPrimitiveSources 'Primitive source upgrade compatibility'
        }
    }

    $modelSourceCount = @($firstModel.Keys | Where-Object {
        $_ -cne 'model-generation-manifest.json'
    }).Count
    if ($modelSourceCount -ne 831) {
        throw "Full model source count mismatch: actual=$modelSourceCount, expected=831."
    }

    [System.Collections.Generic.List[string]] $hashLines = @()
    foreach ($entry in $firstModel.GetEnumerator()) {
        $hashLines.Add("model|$($entry.Key)|$($entry.Value)")
    }
    foreach ($entry in $firstPrimitives.GetEnumerator()) {
        $hashLines.Add("primitive|$($entry.Key)|$($entry.Value)")
    }
    [string[]] $orderedHashLines = $hashLines.ToArray()
    [System.Array]::Sort($orderedHashLines, [System.StringComparer]::Ordinal)
    Write-Utf8NoBomLf (
        Join-Path $output 'artifact-hashes.txt') (($orderedHashLines -join "`n") + "`n")

    $modelManifest = Get-Content -LiteralPath (
        Join-Path $firstModelRoot 'model-generation-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ([string] $modelManifest.compatibility.tool.packageId -cne $packageId -or
        [string] $modelManifest.compatibility.tool.version -cne $toolVersion) {
        throw 'Generated model manifest provenance does not match the installed tool identity.'
    }

    $summary = [ordered]@{
        schemaVersion = 1
        status = 'passed'
        lifecycleOperation = if ($null -eq $previousIdentity) {
            'clean-install-uninstall-reinstall'
        }
        else {
            'clean-install-upgrade-uninstall-reinstall'
        }
        upgradeExecuted = $null -ne $previousIdentity
        previousToolVersion = if ($null -eq $previousIdentity) { $null } else { $previousIdentity.Version }
        generationContractUnchanged = if ($null -eq $previousGenerationContract) {
            $null
        }
        else {
            $previousGenerationContract.Fingerprint -ceq $currentGenerationContract.Fingerprint
        }
        packageId = $packageId
        toolVersion = $toolVersion
        targetFramework = [string] $modelManifest.compatibility.targetFramework
        modelSourceCount = $modelSourceCount
        modelArtifactCount = $firstModel.Count
        primitiveArtifactCount = $firstPrimitives.Count
        packageSha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    Write-Utf8NoBomLf (
        Join-Path $output 'smoke-summary.json') (($summary | ConvertTo-Json -Depth 4) + "`n")
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
