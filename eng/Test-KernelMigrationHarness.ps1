#Requires -Version 7.2
[CmdletBinding()]
param([string] $OutputDirectory = 'artifacts/kernel-k0-harness-tests')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path $output)) { throw 'Use a fresh directory under artifacts/.' }
New-Item -ItemType Directory -Path $output | Out-Null
function Run([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE): $Arguments" }
}
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function ExpectFailure([scriptblock] $Action, [string] $Pattern) {
    $caught = $false
    try { & $Action } catch { $caught = $true; Assert ($_.Exception.Message -like $Pattern) "Unexpected failure: $_" }
    Assert $caught "Expected failure matching $Pattern"
}
function WriteText([string] $Path, [string] $Value) { [IO.File]::WriteAllText($Path, $Value.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false)) }
$pin = Get-Content "$root/eng/kernel-migration-baseline.json" -Raw | ConvertFrom-Json

# A change in the live consumer cannot silently redefine the historical consumer.
$scratchRoot = "$output/fixture-repository"
New-Item -ItemType Directory -Path "$scratchRoot/$($pin.fixture.directory)", "$scratchRoot/Tests/KernelMigration/Consumer" -Force | Out-Null
Copy-Item "$root/$($pin.fixture.directory)/*" "$scratchRoot/$($pin.fixture.directory)"
WriteText "$scratchRoot/Tests/KernelMigration/Consumer/Program.cs" 'throw new Exception("Changed live fixture");'
Copy-KernelBaselineFixture $scratchRoot $pin.fixture "$output/frozen-copy"
Assert ((Get-FileHash "$output/frozen-copy/Program.cs").Hash.ToLowerInvariant() -ceq $pin.fixture.files.'Program.cs') 'Live consumer affected the frozen fixture.'
WriteText "$scratchRoot/$($pin.fixture.directory)/Program.cs" 'throw new Exception("Tampered frozen fixture");'
ExpectFailure { Copy-KernelBaselineFixture $scratchRoot $pin.fixture "$output/tampered-copy" } 'Frozen baseline fixture drift:*'
$badRevision = $pin.fixture | ConvertTo-Json -Depth 5 | ConvertFrom-Json
$badRevision.revision = 'sha256:' + ('0' * 64)
ExpectFailure { Copy-KernelBaselineFixture $root $badRevision "$output/wrong-revision" } 'Frozen fixture content revision mismatch.'

# Final comparison failures must persist failed status even after all previous gates passed.
$approved = "$output/approved"
$actual = "$output/actual"
New-Item -ItemType Directory -Path $approved, $actual | Out-Null
foreach ($name in @('public-api.txt', 'source-inventory.txt', 'package-inventory.txt')) {
    WriteText "$approved/$name" "expected`n"
    WriteText "$actual/$name" "expected`n"
}
WriteText "$actual/package-inventory.txt" "drift`n"
$evidence = New-KernelEvidence -RunRegressionAndSmoke
ExpectFailure {
    Invoke-KernelEvidenceRun $evidence "$output/drift-evidence.json" {
        param($state)
        foreach ($name in @($state.gates.Keys)) {
            Start-KernelGate $state $name
            if ($name -eq 'inventoryComparison') { Assert-KernelInventories $approved $actual }
            Complete-KernelGate $state
        }
    }
} 'Baseline drift: package-inventory.txt'
$saved = Get-Content "$output/drift-evidence.json" -Raw | ConvertFrom-Json
Assert ($saved.status -eq 'failed' -and $saved.gates.inventoryComparison -eq 'failed' -and $saved.failure.gate -eq 'inventoryComparison') 'Drift evidence incorrectly reports success.'

$early = New-KernelEvidence -RunRegressionAndSmoke
ExpectFailure {
    Invoke-KernelEvidenceRun $early "$output/early-evidence.json" { param($state); Start-KernelGate $state 'baselineBuild'; throw 'Restore failed probe' }
} 'Restore failed probe'
$saved = Get-Content "$output/early-evidence.json" -Raw | ConvertFrom-Json
Assert ($saved.status -eq 'failed' -and $saved.gates.oldConsumer -eq 'pending') 'Early failure did not preserve pending gates.'

WriteText "$actual/package-inventory.txt" "expected`n"
foreach ($full in @($false, $true)) {
    $state = New-KernelEvidence -RunRegressionAndSmoke:$full
    Invoke-KernelEvidenceRun $state "$output/success-$full.json" {
        param($state)
        foreach ($name in @($state.gates.Keys)) {
            if ($state.gates[$name] -eq 'skipped') { continue }
            Start-KernelGate $state $name
            if ($name -eq 'inventoryComparison') { Assert-KernelInventories $approved $actual }
            Complete-KernelGate $state
        }
    }
    $expected = if ($full) { 'passed' } else { 'partial' }
    Assert ($state.status -eq $expected) 'Incorrect complete/partial evidence state.'
}

# Real, separate parent Git repositories reproduce the P1 trigger without changing the user's HEAD.
$buildProperties = @(Get-KernelBaselineBuildProperties $pin.sourceRevision)
$nuspecHashes = @()
$implementationHashes = @()
$unfixedLocaleHashes = @()
$hostRevisions = @()
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PackageId>MyFhirSdk.CodeGen.Tool</PackageId>
    <AssemblyName>MyFhirSdk.CodeGen</AssemblyName>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://example.invalid/kernel-test</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <Deterministic>true</Deterministic>
    <PathMap>$(MSBuildProjectDirectory)=/_/kernel-probe</PathMap>
  </PropertyGroup>
</Project>
'@
foreach ($hostName in @('host-a', 'host-b')) {
    $hostRoot = "$output/$hostName"
    $projectRoot = "$hostRoot/exported-source"
    New-Item -ItemType Directory -Path $projectRoot, "$hostRoot/no-hooks" -Force | Out-Null
    Run git @('init', '--quiet', '-b', $hostName, $hostRoot)
    Run git @('-C', $hostRoot, '-c', 'user.name=K0 regression', '-c', 'user.email=k0@example.invalid', '-c', 'commit.gpgsign=false', '-c', "core.hooksPath=$hostRoot/no-hooks", 'commit', '--quiet', '--allow-empty', '-m', $hostName)
    # Exercise the actual archive path; writing LF probe sources alone misses Git
    # working-tree conversions on Windows runners. Include attributed text and binary.
    WriteText "$hostRoot/ArchiveProbe.cs" "public class ArchiveProbe { }`n"
    WriteText "$hostRoot/.gitattributes" "*.txt text`n*.bin -text`n"
    WriteText "$hostRoot/attributed.txt" "attributed text`n"
    $binary = [byte[]] @(0, 13, 10, 255, 10)
    [IO.File]::WriteAllBytes("$hostRoot/probe.bin", $binary)
    Run git @('-C', $hostRoot, '-c', 'core.autocrlf=false', '-c', 'core.eol=lf', 'add', 'ArchiveProbe.cs', '.gitattributes', 'attributed.txt', 'probe.bin')
    Run git @('-C', $hostRoot, '-c', 'user.name=K0 regression', '-c', 'user.email=k0@example.invalid', '-c', 'commit.gpgsign=false', '-c', "core.hooksPath=$hostRoot/no-hooks", 'commit', '--quiet', '-m', 'Archive inputs')
    $callerAutoCrlf = if ($hostName -eq 'host-a') { 'true' } else { 'false' }
    Run git @('-C', $hostRoot, 'config', 'core.autocrlf', $callerAutoCrlf)
    Run git @('-C', $hostRoot, 'config', 'core.eol', 'crlf')
    $hostRevision = (& git -C $hostRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read test host revision.' }
    $hostRevisions += $hostRevision
    $export = "$hostRoot/archive-check"
    New-Item -ItemType Directory -Path $export | Out-Null
    Run git @('-C', $hostRoot, 'archive', '--format=tar', "--output=$hostRoot/unfixed.tar", $hostRevision)
    Run tar @('-xf', "$hostRoot/unfixed.tar", '-C', $export)
    Assert ([IO.File]::ReadAllText("$export/attributed.txt").Contains("`r`n")) 'Archive probe did not reproduce host line-ending conversion.'
    Export-KernelBaselineSource $hostRoot $hostRevision "$hostRoot/canonical.tar"
    Run tar @('-xf', "$hostRoot/canonical.tar", '-C', $export)
    Assert ([IO.File]::ReadAllText("$export/ArchiveProbe.cs") -ceq "public class ArchiveProbe { }`n") 'Archive source depends on core.autocrlf.'
    Assert ([IO.File]::ReadAllText("$export/attributed.txt") -ceq "attributed text`n") 'Archive text depends on core.eol.'
    Assert ([Convert]::ToHexString([IO.File]::ReadAllBytes("$export/probe.bin")) -ceq [Convert]::ToHexString($binary)) 'Archive changed binary bytes.'
    Assert ((& git -C $hostRoot config core.autocrlf) -ceq $callerAutoCrlf) 'Export changed caller autocrlf configuration.'
    Assert ((& git -C $hostRoot config core.eol) -ceq 'crlf') 'Export changed caller eol configuration.'
    # Share the real TFM without hard-coding a production contract in this probe.
    [xml] $props = Get-Content "$root/Directory.Build.props" -Raw
    $tfm = [string] $props.Project.PropertyGroup.MyFhirSdkTargetFramework
    WriteText "$projectRoot/Probe.csproj" ($project.Replace('net9.0', $tfm))
    WriteText "$projectRoot/Probe.cs" 'public class Probe {}'
    if ($hostName -eq 'host-a') {
        Run dotnet @('pack', "$projectRoot/Probe.csproj", '-c', 'Release', '-o', "$hostRoot/unfixed")
        $unfixed = @(Get-ChildItem "$hostRoot/unfixed" -Filter *.nupkg)
        ExpectFailure { Assert-KernelPackageProvenance $unfixed[0].FullName $pin.sourceRevision } 'Package repository provenance does not match*'
    }
    $previousLanguage = [Environment]::GetEnvironmentVariable('DOTNET_CLI_UI_LANGUAGE')
    $callerLanguage = if ($hostName -eq 'host-a') { 'zh-TW' } else { 'en-US' }
    try {
        [Environment]::SetEnvironmentVariable('DOTNET_CLI_UI_LANGUAGE', $callerLanguage)
        # Hold source and Git metadata constant but expose the localized generated
        # comment before applying the canonical-language wrapper. Rebuild avoids caches.
        Run dotnet (@('build', "$projectRoot/Probe.csproj", '-c', 'Release', '-t:Rebuild') + $buildProperties)
        $unfixedLocaleHashes += (Get-FileHash "$projectRoot/bin/Release/$tfm/MyFhirSdk.CodeGen.dll").Hash
        $referenceHash = (Get-FileHash "$projectRoot/obj/Release/$tfm/ref/MyFhirSdk.CodeGen.dll").Hash
        Invoke-KernelDotNet -Arguments (@('build', "$projectRoot/Probe.csproj", '-c', 'Release', '-t:Rebuild') + $buildProperties)
        Assert ([Environment]::GetEnvironmentVariable('DOTNET_CLI_UI_LANGUAGE') -ceq $callerLanguage) 'Canonical build leaked its locale into the caller.'
        $implementationHashes += (Get-FileHash "$projectRoot/bin/Release/$tfm/MyFhirSdk.CodeGen.dll").Hash
        Assert ((Get-FileHash "$projectRoot/obj/Release/$tfm/ref/MyFhirSdk.CodeGen.dll").Hash -ceq $referenceHash) 'Language normalization changed the reference contract.'
        Invoke-KernelDotNet -Arguments (@('pack', "$projectRoot/Probe.csproj", '-c', 'Release', '--no-build', '--no-restore', '-o', "$hostRoot/fixed") + $buildProperties)
        ExpectFailure { Invoke-KernelDotNet -Arguments @('build', "$hostRoot/missing.csproj") } 'dotnet failed*'
        Assert ([Environment]::GetEnvironmentVariable('DOTNET_CLI_UI_LANGUAGE') -ceq $callerLanguage) 'Failed build did not restore the caller locale.'
    }
    finally { [Environment]::SetEnvironmentVariable('DOTNET_CLI_UI_LANGUAGE', $previousLanguage) }
    $package = @(Get-ChildItem "$hostRoot/fixed" -Filter *.nupkg)
    Assert-KernelPackageProvenance $package[0].FullName $pin.sourceRevision
    $assemblyInfo = Get-Content "$projectRoot/obj/Release/$tfm/Probe.AssemblyInfo.cs" -Raw
    Assert ($assemblyInfo.Contains('1.0.0+' + $pin.sourceRevision) -and -not $assemblyInfo.Contains($hostRevision)) 'Assembly informational version leaked host HEAD.'
    $zip = [IO.Compression.ZipFile]::OpenRead($package[0].FullName)
    try {
        $stream = $zip.GetEntry('MyFhirSdk.CodeGen.Tool.nuspec').Open()
        try { $nuspecHashes += [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
    } finally { $zip.Dispose() }
}
Assert ($hostRevisions[0] -cne $hostRevisions[1]) 'Test hosts must have different HEADs.'
Assert ($nuspecHashes[0] -ceq $nuspecHashes[1]) 'Package metadata depends on host HEAD/branch.'
Assert ($unfixedLocaleHashes[0] -cne $unfixedLocaleHashes[1]) 'Locale probe did not reproduce the original DLL hash mismatch.'
Assert ($implementationHashes[0] -ceq $implementationHashes[1]) 'Canonical DLL bytes depend on caller language or host path.'

# A real assembly hash failure must expose both sides in the persisted failure evidence.
$probeAssembly = "$projectRoot/bin/Release/$tfm/MyFhirSdk.CodeGen.dll"
$badPin = [pscustomobject]@{
    sdkAssemblyIdentity = [Reflection.AssemblyName]::GetAssemblyName($probeAssembly).FullName
    sdkImplementationSha256 = '0' * 64
}
$hashFailure = New-KernelEvidence -RunRegressionAndSmoke
ExpectFailure {
    Invoke-KernelEvidenceRun $hashFailure "$output/hash-failure-evidence.json" {
        param($state)
        Start-KernelGate $state 'baselineBuild'
        Assert-KernelSdkBaseline $probeAssembly $badPin $state
    }
} 'Pinned SDK implementation SHA-256 mismatch. Expected:*actual:*'
$saved = Get-Content "$output/hash-failure-evidence.json" -Raw | ConvertFrom-Json
Assert ($saved.status -eq 'failed' -and $saved.sdkBaselineComparison.actualSha256 -ceq $implementationHashes[1].ToLowerInvariant()) 'Failure evidence lost the actual DLL hash.'
$badPin.sdkAssemblyIdentity = 'Wrong, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'
ExpectFailure { Assert-KernelSdkBaseline $probeAssembly $badPin ([ordered]@{}) } 'Pinned SDK assembly identity mismatch. Expected:*actual:*'
Write-KernelJson "$output/summary.json" ([ordered]@{
    status = 'passed'; independentHostMetadata = $true; frozenFixtureTamperRejected = $true
    liveFixtureIsIndependent = $true; failedEvidencePreserved = $true; partialAndPassedStatesVerified = $true
    independentCallerLanguageAndPath = $true; languageRestoredOnFailure = $true; actualHashInFailureEvidence = $true
    archiveIndependentOfGitLineEndings = $true; archiveBinaryBytesPreserved = $true
})
Write-Output 'K0 harness regression tests passed.'
# Expected native failures leave LASTEXITCODE nonzero. Actions' pwsh wrapper uses
# that value after this script returns; report success only after every assertion.
exit 0
