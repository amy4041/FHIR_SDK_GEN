#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SmokeDirectory,
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [string] $RuntimeReferenceAssetPath,
    [string] $OutputDirectory = 'artifacts/kernel-k6/generated-sdk'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
. (Join-Path $PSScriptRoot 'GeneratedSdkIntegration.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path -LiteralPath $output)) {
    throw 'Use a fresh output directory under artifacts/.'
}
New-Item -ItemType Directory -Path $output | Out-Null
$SmokeDirectory = (Resolve-Path -LiteralPath $SmokeDirectory).Path
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$RuntimeReferenceAssetPath = (Resolve-Path -LiteralPath $RuntimeReferenceAssetPath).Path
$workspace = Join-Path ([IO.Path]::GetTempPath()) ('MyFhirSdk-K6-Sdk-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $workspace 'source'
$savedEnvironment = @{}
$evidence = [ordered]@{
    schemaVersion = 1; status = 'running'; activeGate = $null; failure = $null
    gates = [ordered]@{ installedToolInputs = 'pending'; cleanSourceStage = 'pending'; sdkCompositionRegression = 'pending'; generatedInputsUnchanged = 'pending' }
    modelsCli = 'pending'; sdkComposition = 'pending'
}
try {
    Invoke-KernelEvidenceRun -Evidence $evidence -OutputPath "$output/evidence.json" -Action {
        param($evidence)
        Start-KernelGate $evidence 'installedToolInputs'
        $inputs = Assert-GeneratedSdkInputs $SmokeDirectory $PackagePath $RuntimeReferenceAssetPath
        $evidence.modelsCli = 'passed'
        foreach ($field in @('packageSha256', 'fhirPackageSha256', 'primitivePolicySha256', 'packageId', 'toolVersion', 'targetFramework')) {
            $evidence[$field] = $inputs.summary.$field
        }
        $evidence.compilerReferenceSha256 = $inputs.referenceSha256
        $evidence.generatedSourceCount = 852
        [IO.File]::WriteAllText("$output/artifact-hashes.txt", ($inputs.hashes -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
        Complete-KernelGate $evidence

        Start-KernelGate $evidence 'cleanSourceStage'
        if ($workspace.StartsWith($root + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'SDK staging must be outside the checkout.' }
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        # Include tracked and non-ignored untracked working-tree files, excluding generated output and caches.
        $sources = @(Get-GeneratedSdkSourceFiles $root)
        $copied = [Collections.Generic.List[string]]::new()
        foreach ($relative in $sources) {
            $source = Join-Path $root $relative
            if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source links are unsupported: $relative" }
            $destination = [IO.Path]::GetFullPath($relative, $stage)
            if (-not $destination.StartsWith($stage + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'Source path escapes staging.' }
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination
            $copied.Add($relative)
        }
        if (Test-Path -LiteralPath "$stage/Generated/R5") { throw 'Committed generated sources leaked into staging.' }
        if (@(Get-ChildItem -LiteralPath $stage -Recurse -Directory -Force | Where-Object { $_.Name -in @('bin', 'obj', '.git') }).Count) { throw 'Staged source is not clean.' }
        New-Item -ItemType Directory -Path "$stage/Generated" -Force | Out-Null
        Copy-Item -LiteralPath $inputs.generatedRoot -Destination "$stage/Generated/R5" -Recurse
        if ((@(Get-GeneratedSdkHashes "$stage/Generated/R5") -join "`n") -cne ($inputs.hashes -join "`n")) { throw 'Staged SDK generated inputs changed.' }
        [IO.File]::WriteAllLines("$output/staged-source-files.txt", $copied.ToArray(), [Text.UTF8Encoding]::new($false))
        $evidence.cleanSourceStage = [ordered]@{ outsideCheckout = $true; reusedBinObj = $false; reusedCommittedGeneratedSources = $false; reusedNuGetCache = $false; sourceSelection = 'tracked-and-nonignored-untracked'; sourceFiles = $copied.Count }
        Complete-KernelGate $evidence

        Start-KernelGate $evidence 'sdkCompositionRegression'
        $isolatedEnvironment = @{
            DOTNET_CLI_HOME = "$workspace/cli-home"; NUGET_PACKAGES = "$workspace/nuget-packages"
            NUGET_HTTP_CACHE_PATH = "$workspace/nuget-http-cache"; DOTNET_CLI_TELEMETRY_OPTOUT = '1'
            NUGET_PLUGINS_CACHE_PATH = "$workspace/nuget-plugins-cache"; NUGET_SCRATCH = "$workspace/nuget-scratch"
            DOTNET_NOLOGO = '1'; DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        }
        foreach ($name in $isolatedEnvironment.Keys) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $isolatedEnvironment[$name], 'Process')
        }
        $reference = "$workspace/canonical/MyFhirSdk.Runtime.dll"
        New-Item -ItemType Directory -Path "$workspace/canonical" | Out-Null
        Copy-Item -LiteralPath $RuntimeReferenceAssetPath -Destination $reference
        # Existing strict TRX, rebuilt-dependent-DLL, deployment and missing-Runtime gates all run against this SDK.
        Push-Location $stage
        try {
            & (Join-Path $PSHOME 'pwsh') -NoProfile -File "$stage/eng/Test-KernelConsumerRebuild.ps1" -OutputDirectory artifacts/k6-rebuild -RuntimeReferenceAssetPath $reference *> "$output/sdk-rebuild.log"
        } finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw "Fresh generated SDK rebuild failed. See $output/sdk-rebuild.log" }
        $rebuilt = Get-Content -LiteralPath "$stage/artifacts/k6-rebuild/evidence.json" -Raw -Encoding utf8 | ConvertFrom-Json
        if ($rebuilt.status -cne 'passed' -or @($rebuilt.gates.PSObject.Properties | Where-Object { $_.Value -cne 'passed' }).Count) { throw 'Incomplete fresh SDK regression/deployment gates.' }
        $evidence.regressionCounts = $rebuilt.regressionCounts
        $evidence.regressionProjects = $rebuilt.regressionProjects
        $evidence.externalClientSmoke = if ($rebuilt.regressionCounts.skipped -eq 1) { 'skipped-no-integration-base-url' } else { 'passed' }
        $evidence.sdkComposition = 'passed'
        $evidence.deployment = $rebuilt.deployment
        $evidence.missingRuntime = $rebuilt.missingRuntime
        Complete-KernelGate $evidence

        Start-KernelGate $evidence 'generatedInputsUnchanged'
        $after = Assert-GeneratedSdkInputs $SmokeDirectory $PackagePath $RuntimeReferenceAssetPath
        if ((Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash.ToLowerInvariant() -cne $inputs.referenceSha256) { throw 'SDK build changed the canonical compiler reference.' }
        if (($after.hashes -join "`n") -cne ($inputs.hashes -join "`n") -or
            (@(Get-GeneratedSdkHashes "$stage/Generated/R5") -join "`n") -cne ($inputs.hashes -join "`n")) { throw 'SDK build changed generated inputs.' }
        Complete-KernelGate $evidence
    }
} finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    # Preserve diagnostics even when a staged restore/build/test fails.
    Save-GeneratedSdkDiagnostics -RebuildDirectory "$stage/artifacts/k6-rebuild" -OutputDirectory $output
    if (Test-Path -LiteralPath $workspace) {
        $resolved = [IO.Path]::GetFullPath($workspace)
        $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        if ([IO.Path]::GetDirectoryName($resolved) -ine $temp -or [IO.Path]::GetFileName($resolved) -notlike 'MyFhirSdk-K6-Sdk-*') { throw 'Refusing unexpected SDK staging cleanup.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Write-Output "K6 generated SDK evidence: $output/evidence.json"
