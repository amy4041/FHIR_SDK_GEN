#Requires -Version 7.2
[CmdletBinding()]
param([string] $OutputDirectory = 'artifacts/kernel-k7/rollback-harness', [string] $CurrentRegressionDirectory = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
. (Join-Path $PSScriptRoot 'KernelSourceRollback.Common.ps1')
. (Join-Path $PSScriptRoot 'KernelConsumerRebuild.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path -LiteralPath $output)) { throw 'Use a fresh output directory under artifacts/.' }
New-Item -ItemType Directory -Path $output | Out-Null
$pin = Get-Content "$root/eng/kernel-migration-baseline.json" -Raw -Encoding utf8 | ConvertFrom-Json
$checks = [Collections.Generic.List[string]]::new()
$result = [ordered]@{ schemaVersion = 1; status = 'running'; checks = @(); failure = $null }
function Reject([string] $Name, [scriptblock] $Mutation, [string] $Message) {
    $fixture = "$output/$Name/baseline"
    New-Item -ItemType Directory -Path $fixture -Force | Out-Null
    $baseline = [ordered]@{ status = 'passed'; sourceRevision = $pin.sourceRevision; gates = [ordered]@{} }
    foreach ($gate in @('baselineBuild', 'oldConsumer', 'currentConsumer', 'apiInventory', 'sourceInventory', 'regression', 'primitiveEquivalence', 'toolSmoke', 'inventoryComparison')) { $baseline.gates[$gate] = 'passed' }
    & $Mutation $baseline $fixture
    Write-KernelJson "$fixture/evidence.json" $baseline
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/Test-KernelSourceRollback.ps1" -BaselineDirectory $fixture -OutputDirectory "$output/$Name/run" *> "$output/$Name/process.log"
    if ($LASTEXITCODE -eq 0) { throw "Rollback probe unexpectedly succeeded: $Name" }
    $evidence = Get-Content "$output/$Name/run/evidence.json" -Raw -Encoding utf8 | ConvertFrom-Json
    if ($evidence.status -cne 'failed' -or $evidence.gates.baseline -cne 'failed' -or -not $evidence.failure.message.Contains($Message)) { throw "Rollback probe failed without the expected evidence: $Name" }
    $checks.Add($Name)
}
try {
    $currentAssemblies = @(Get-KernelRegressionAssemblies)
    $historicalAssemblies = @(Get-KernelRegressionAssemblies -Historical)
    if ($currentAssemblies.Count -ne 9 -or $currentAssemblies -cnotcontains 'MyFhirSdk.FhirVersions.V5.Tests.dll' -or
        $currentAssemblies -cnotcontains 'MyFhirSdk.FhirVersions.V6.Tests.dll' -or
        $historicalAssemblies.Count -ne 7 -or $historicalAssemblies -ccontains 'MyFhirSdk.FhirVersions.V5.Tests.dll') {
        throw 'Current and historical regression assembly inventories are incorrect.'
    }
    foreach ($set in @(@{ name = 'current'; assemblies = $currentAssemblies }, @{ name = 'historical'; assemblies = $historicalAssemblies })) {
        $reports = "$output/$($set.name)-reports"
        New-Item -ItemType Directory -Path $reports | Out-Null
        foreach ($assembly in $set.assemblies) {
            $text = @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions><UnitTest id="1"><TestMethod codeBase="$assembly" /></UnitTest></TestDefinitions>
  <Results><UnitTestResult testId="1" testName="Probe.Passes" outcome="Passed" /></Results>
  <ResultSummary><Counters total="1" passed="1" failed="0" /></ResultSummary>
</TestRun>
"@
            [IO.File]::WriteAllText("$reports/$assembly.trx", $text, [Text.UTF8Encoding]::new($false))
        }
        $accepted = Assert-KernelConsumerRegression $reports $set.assemblies
        if ($accepted.projects.Count -ne $set.assemblies.Count) { throw 'Regression report set was incomplete.' }
        $checks.Add("$($set.name)-regression-assembly-set-accepted")
        $caught = $false
        try {
            $wrong = if ($set.name -eq 'current') { $historicalAssemblies } else { $currentAssemblies }
            Assert-KernelConsumerRegression $reports $wrong | Out-Null
        } catch {
            if ($_.Exception.Message -notmatch 'Unexpected test assembly:|Missing test assembly report:') { throw }
            $caught = $true
        }
        if (-not $caught) { throw 'Mismatched regression assembly set was accepted.' }
        $checks.Add("$($set.name)-regression-wrong-assembly-set-rejected")
    }
    if ($CurrentRegressionDirectory) {
        $accepted = Assert-KernelConsumerRegression $CurrentRegressionDirectory $currentAssemblies
        if ($accepted.projects.Count -ne $currentAssemblies.Count) { throw 'Expected all current regression assemblies.' }
        $checks.Add('current-v5-regression-reports-accepted')
    }
    $fingerprint = Get-KernelCurrentSourceFingerprint $root
    $saved = [pscustomobject]@{ currentSourceFingerprint = [pscustomobject] $fingerprint }
    if (-not (Test-KernelRegressionFingerprint $saved $fingerprint)) { throw 'Matching fingerprint was not reusable.' }
    $checks.Add('matching-current-source-fingerprint')
    if (Test-KernelRegressionFingerprint ([pscustomobject]@{}) $fingerprint) { throw 'Missing fingerprint was reused.' }
    $checks.Add('legacy-baseline-without-fingerprint-requires-rerun')
    $probe = Join-Path $root ('Tests/Architecture/K7FingerprintProbe_' + [Guid]::NewGuid().ToString('N') + '.cs')
    try {
        [IO.File]::WriteAllText($probe, '#error Changed current source must trigger a new regression')
        if (Test-KernelRegressionFingerprint $saved (Get-KernelCurrentSourceFingerprint $root)) { throw 'Changed current source was reused.' }
        $checks.Add('untracked-current-source-change-requires-rerun')
    } finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
    $consumer = "$output/consumer-fixture"
    New-Item -ItemType Directory -Path $consumer | Out-Null
    [IO.File]::WriteAllText("$consumer/Consumer.dll", 'original consumer')
    $expectedHash = (Get-FileHash "$consumer/Consumer.dll").Hash.ToLowerInvariant()
    Assert-KernelRollbackConsumer $consumer $expectedHash
    [IO.File]::WriteAllText("$consumer/Consumer.dll", 'replacement consumer')
    $caught = $false
    try { Assert-KernelRollbackConsumer $consumer $expectedHash } catch {
        if (-not $_.Exception.Message.Contains('consumer binary hash mismatch')) { throw }
        $caught = $true
    }
    if (-not $caught) { throw 'Replaced consumer was accepted.' }
    $checks.Add('replacement-consumer-rejected')
    [IO.File]::WriteAllText("$consumer/run.log", '')
    $caught = $false
    try { Assert-KernelRollbackConsumerOutput "$consumer/run.log" } catch {
        if (-not $_.Exception.Message.Contains('behavior output mismatch')) { throw }
        $caught = $true
    }
    if (-not $caught) { throw 'Silent success was accepted.' }
    $checks.Add('exit-zero-without-behavior-proof-rejected')
    [IO.File]::WriteAllText("$consumer/run.log", "Consumer passed: primitive access, model/base assignment, JSON round-trip.`n")
    Assert-KernelRollbackConsumerOutput "$consumer/run.log"
    $checks.Add('expected-consumer-behavior-output')
    Reject 'missing-baseline-gate' { param($b, $f); $b.gates.Remove('toolSmoke') } 'toolSmoke'
    Reject 'skipped-baseline-gate' { param($b, $f); $b.gates.regression = 'skipped' } 'regression'
    Reject 'different-source-pin' { param($b, $f); $b.sourceRevision = '0000000000000000000000000000000000000000' } 'immutable K0'
    Reject 'runtime-project-not-reverted' {
        param($b, $f)
        New-Item -ItemType Directory -Path "$f/baseline-source/Runtime" -Force | Out-Null
        [IO.File]::WriteAllText("$f/baseline-source/Runtime/MyFhirSdk.Runtime.csproj", '<Project />')
    } 'extracted Runtime project'
    Reject 'different-sdk-implementation' {
        param($b, $f)
        [xml] $props = Get-Content "$root/Directory.Build.props" -Raw
        $tfm = [string] $props.Project.PropertyGroup.MyFhirSdkTargetFramework
        New-Item -ItemType Directory -Path "$f/baseline-source/bin/Release/$tfm" -Force | Out-Null
        Copy-Item -LiteralPath "$root/Directory.Build.props" -Destination "$f/baseline-source/Directory.Build.props"
        [IO.File]::WriteAllText("$f/baseline-source/bin/Release/$tfm/MyFhirSdk.dll", 'not the pinned SDK')
    } 'hash mismatch'
    $result.status = 'passed'
} catch {
    $result.status = 'failed'; $result.failure = $_.Exception.Message
    throw
} finally {
    $result.checks = $checks.ToArray()
    Write-KernelJson "$output/evidence.json" $result
}
Write-Output "K7 rollback harness: $($checks.Count) checks passed."
# Expected failing child processes must not leak their exit code into the Actions wrapper.
exit 0
