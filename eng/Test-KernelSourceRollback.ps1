#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/kernel-k7/rollback',
    [string] $BaselineDirectory = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
. (Join-Path $PSScriptRoot 'KernelConsumerRebuild.Common.ps1')
. (Join-Path $PSScriptRoot 'KernelSourceRollback.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$artifactPrefix = (Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($artifactPrefix, $comparison) -or (Test-Path -LiteralPath $output)) { throw 'Use a fresh output directory under artifacts/.' }
New-Item -ItemType Directory -Path $output | Out-Null
$pin = Get-Content "$root/eng/kernel-migration-baseline.json" -Raw -Encoding utf8 | ConvertFrom-Json
$evidence = [ordered]@{
    schemaVersion = 1; status = 'running'; activeGate = $null; failure = $null
    policy = 'pre-release-whole-source-rollback'; sourceRevision = $pin.sourceRevision
    gates = [ordered]@{ baseline = 'pending'; currentSourceRegression = 'pending'; restoredSourceRegression = 'pending'; restoredDeployment = 'pending' }
    currentCheckoutModified = $false; mainHistoryRewritten = $false
}
Invoke-KernelEvidenceRun -Evidence $evidence -OutputPath "$output/evidence.json" -Action {
    param($evidence)
    Start-KernelGate $evidence 'baseline'
    $baseline = if ($BaselineDirectory) { [IO.Path]::GetFullPath($BaselineDirectory, $root) } else { "$output/baseline" }
    if (-not $baseline.StartsWith($artifactPrefix, $comparison)) { throw 'Baseline must be an isolated directory under artifacts/.' }
    if (-not $BaselineDirectory) {
        & (Join-Path $PSHOME 'pwsh') -NoProfile -File "$root/eng/Test-KernelMigrationBaseline.ps1" -OutputDirectory $baseline -RunRegressionAndSmoke *> "$output/baseline.log"
        if ($LASTEXITCODE -ne 0) { throw "Baseline reconstruction failed; see $output/baseline.log" }
    }
    $previous = Get-Content "$baseline/evidence.json" -Raw -Encoding utf8 | ConvertFrom-Json
    foreach ($gate in @('baselineBuild', 'oldConsumer', 'currentConsumer', 'apiInventory', 'sourceInventory', 'regression', 'primitiveEquivalence', 'toolSmoke', 'inventoryComparison')) {
        if ($previous.gates.$gate -cne 'passed') { throw "Baseline gate missing or failed: $gate" }
    }
    if ($previous.status -cne 'passed' -or $previous.sourceRevision -cne $pin.sourceRevision) { throw 'Expected a successful reconstruction of the immutable K0 source revision.' }
    $source = "$baseline/baseline-source"
    if (Test-Path -LiteralPath "$source/Runtime/MyFhirSdk.Runtime.csproj") { throw 'Rollback source still contains the extracted Runtime project.' }
    [xml] $props = Get-Content "$source/Directory.Build.props" -Raw
    $tfm = [string] $props.Project.PropertyGroup.MyFhirSdkTargetFramework
    $sdk = "$source/bin/Release/$tfm/MyFhirSdk.dll"
    $reference = "$source/CodeGen/bin/Release/$tfm/Assets/RuntimeReferences/$tfm/MyFhirSdk.dll"
    $rollbackHashes = @(@($sdk, $pin.sdkImplementationSha256), @($reference, $pin.compilerReferenceSha256), @("$source/CodeGen/Policy/runtime-contract.json", $pin.descriptorSha256))
    foreach ($pair in $rollbackHashes) {
        if ((Get-FileHash -LiteralPath $pair[0]).Hash.ToLowerInvariant() -cne $pair[1]) { throw "Rollback input hash mismatch: $($pair[0])" }
    }
    Assert-KernelInventories "$root/docs/gen/baselines/kernel-k0" $baseline
    $evidence.targetFramework = $tfm
    Assert-KernelRollbackConsumer "$baseline/old-consumer" ([string] $previous.oldConsumerSha256)
    $evidence.toolVersion = $pin.toolVersion
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'currentSourceRegression'
    $currentFingerprint = Get-KernelCurrentSourceFingerprint $root
    if (Test-KernelRegressionFingerprint $previous $currentFingerprint) {
        $currentReports = "$baseline/test-results"
        $evidence.currentRegressionOperation = 'reused-matching-source-fingerprint'
    } else {
        $currentReports = "$output/current-test-results"
        $evidence.currentRegressionOperation = 'rerun-missing-or-stale-source-fingerprint'
        Push-Location $root
        try {
            Invoke-KernelDotNet @('test', "$root/MyFhirSdk.sln", '-c', 'Release', '--logger', 'trx;LogFilePrefix=k7-current', '--results-directory', $currentReports) *> "$output/current-source-regression.log"
        } finally { Pop-Location }
    }
    $currentRegression = Assert-KernelConsumerRegression $currentReports (Get-KernelRegressionAssemblies)
    if ((Get-KernelCurrentSourceFingerprint $root).sha256 -cne $currentFingerprint.sha256) { throw 'Current source changed during rollback regression.' }
    $evidence.currentSourceFingerprint = $currentFingerprint
    $evidence.currentRegressionCounts = $currentRegression.counts
    $evidence.currentRegressionProjects = $currentRegression.projects
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'restoredSourceRegression'
    $properties = @(Get-KernelBaselineBuildProperties $pin.sourceRevision)
    Push-Location $source
    try {
        if ((& dotnet --version).Trim() -cne $pin.sdkVersion -or $LASTEXITCODE -ne 0) { throw 'Rollback SDK version mismatch.' }
        # K0's regression gate tests CURRENT source; this additional gate explicitly tests the restored OLD source.
        Invoke-KernelDotNet (@('test', "$source/MyFhirSdk.sln", '-c', 'Release', '--logger', 'trx;LogFilePrefix=k7-rollback', '--results-directory', "$output/test-results") + $properties) *> "$output/restored-source-regression.log"
    } finally { Pop-Location }
    $regression = Assert-KernelConsumerRegression "$output/test-results" (Get-KernelRegressionAssemblies -Historical)
    $evidence.restoredRegressionCounts = $regression.counts
    $evidence.restoredRegressionProjects = $regression.projects
    $evidence.externalClientSmoke = if ($regression.counts.skipped -eq 1) { 'skipped-no-integration-base-url' } else { 'passed' }
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'restoredDeployment'
    foreach ($pair in $rollbackHashes) {
        if ((Get-FileHash -LiteralPath $pair[0]).Hash.ToLowerInvariant() -cne $pair[1]) { throw "Restored source regression changed a pinned rollback asset: $($pair[0])" }
    }
    $deployment = "$baseline/old-consumer"
    if (Test-Path -LiteralPath "$deployment/MyFhirSdk.Runtime.dll") { throw 'Historical monolithic deployment must not require the extracted Runtime DLL.' }
    if ((Get-FileHash "$deployment/MyFhirSdk.dll").Hash.ToLowerInvariant() -cne $pin.sdkImplementationSha256) { throw 'Rollback deployment does not contain the restored SDK.' }
    Assert-KernelRollbackConsumer $deployment ([string] $previous.oldConsumerSha256)
    Push-Location $deployment
    try { Invoke-KernelDotNet @("$deployment/Consumer.dll") *> "$output/restored-deployment.log" } finally { Pop-Location }
    Assert-KernelRollbackConsumerOutput "$output/restored-deployment.log"
    $evidence.oldConsumerSha256 = $previous.oldConsumerSha256
    $evidence.sdkImplementationSha256 = $pin.sdkImplementationSha256
    $evidence.compilerReferenceSha256 = $pin.compilerReferenceSha256
    $evidence.descriptorSha256 = $pin.descriptorSha256
    $evidence.deployment = 'historical-consumer-with-rebuilt-monolithic-sdk'
    Complete-KernelGate $evidence
}
Write-Output "K7 source rollback evidence: $output/evidence.json"
