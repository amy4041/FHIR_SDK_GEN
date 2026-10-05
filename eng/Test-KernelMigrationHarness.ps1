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
  </PropertyGroup>
</Project>
'@
foreach ($hostName in @('host-a', 'host-b')) {
    $hostRoot = "$output/$hostName"
    $projectRoot = "$hostRoot/exported-source"
    New-Item -ItemType Directory -Path $projectRoot, "$hostRoot/no-hooks" -Force | Out-Null
    Run git @('init', '--quiet', '-b', $hostName, $hostRoot)
    Run git @('-C', $hostRoot, '-c', 'user.name=K0 regression', '-c', 'user.email=k0@example.invalid', '-c', 'commit.gpgsign=false', '-c', "core.hooksPath=$hostRoot/no-hooks", 'commit', '--quiet', '--allow-empty', '-m', $hostName)
    $hostRevision = (& git -C $hostRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read test host revision.' }
    $hostRevisions += $hostRevision
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
    Run dotnet (@('pack', "$projectRoot/Probe.csproj", '-c', 'Release', '-o', "$hostRoot/fixed") + $buildProperties)
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
Write-KernelJson "$output/summary.json" ([ordered]@{
    status = 'passed'; independentHostMetadata = $true; frozenFixtureTamperRejected = $true
    liveFixtureIsIndependent = $true; failedEvidencePreserved = $true; partialAndPassedStatesVerified = $true
})
Write-Output 'K0 harness regression tests passed.'
