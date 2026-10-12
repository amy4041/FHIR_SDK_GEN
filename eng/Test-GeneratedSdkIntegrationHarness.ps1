#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SmokeDirectory,
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [string] $RuntimeReferenceAssetPath,
    [string] $OutputDirectory = 'artifacts/kernel-k6/input-harness'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
. (Join-Path $PSScriptRoot 'GeneratedSdkIntegration.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path -LiteralPath $output)) { throw 'Use a fresh output directory under artifacts/.' }
New-Item -ItemType Directory -Path $output | Out-Null
$checks = [Collections.Generic.List[string]]::new()
$result = [ordered]@{ schemaVersion = 1; status = 'running'; checks = @(); failure = $null }
function Fixture([string] $Name) {
    $destination = Join-Path $output $Name
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($name in @('generated', 'artifact-hashes.txt', 'smoke-summary.json')) {
        Copy-Item -LiteralPath (Join-Path $SmokeDirectory $name) -Destination $destination -Recurse
    }
    return $destination
}
function Reject([string] $Name, [scriptblock] $Mutation, [string] $Expected) {
    $fixture = Fixture $Name
    & $Mutation $fixture
    $message = $null
    try { Assert-GeneratedSdkInputs $fixture $PackagePath $RuntimeReferenceAssetPath | Out-Null } catch { $message = $_.Exception.Message }
    if (-not $message -or -not $message.Contains($Expected)) { throw "Probe $Name did not reject as expected: $message" }
    $checks.Add($Name)
}
function RefreshHashes([string] $Directory) {
    [IO.File]::WriteAllText("$Directory/artifact-hashes.txt", (@(Get-GeneratedSdkHashes "$Directory/generated/Generated/R5") -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
}
function WriteFixtureJson([string] $Path, [object] $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 30) + "`n", [Text.UTF8Encoding]::new($false))
}
try {
    $probe = Join-Path $root ('Tests/Architecture/K6SourceProbe_' + [Guid]::NewGuid().ToString('N') + '.cs')
    try {
        [IO.File]::WriteAllText($probe, '// Untracked source selection probe', [Text.UTF8Encoding]::new($false))
        $sourceFiles = @(Get-GeneratedSdkSourceFiles $root)
        $relativeProbe = [IO.Path]::GetRelativePath($root, $probe).Replace('\', '/')
        if ($sourceFiles -cnotcontains $relativeProbe -or $sourceFiles -cnotcontains 'MyFhirSdk.csproj' -or
            @($sourceFiles | Where-Object { $_ -match '(^|/)(artifacts|bin|obj|\.git)(/|$)|^Generated/R5/' }).Count) {
            throw 'Working-tree source selection missed new files or included excluded inputs.'
        }
        $checks.Add('tracked-and-untracked-sources-with-output-exclusions')
    } finally {
        if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe }
    }
    $diagnostics = Join-Path $output 'diagnostic-fixture'
    $preserved = Join-Path $output 'preserved-diagnostics'
    New-Item -ItemType Directory -Path "$diagnostics/test-results", $preserved | Out-Null
    $reports = @('evidence.json', 'consumer-inventory.json', 'current-consumer-run.json', 'deployment-run.json', 'missing-runtime-run.json')
    foreach ($name in $reports) {
        [IO.File]::WriteAllText("$diagnostics/$name", '{"status":"failed","stderr":"unexpected deployment failure"}', [Text.UTF8Encoding]::new($false))
    }
    [IO.File]::WriteAllText("$diagnostics/test-results/probe.trx", '<TestRun />')
    Save-GeneratedSdkDiagnostics $diagnostics $preserved
    foreach ($name in $reports) {
        $saved = if ($name -eq 'evidence.json') { 'consumer-rebuild-evidence.json' } else { $name }
        if ((Get-FileHash "$diagnostics/$name").Hash -cne (Get-FileHash "$preserved/$saved").Hash) { throw "Lost diagnostic report: $name" }
    }
    if (-not (Test-Path -LiteralPath "$preserved/test-results/probe.trx")) { throw 'Lost partial regression report.' }
    $checks.Add('failure-evidence-process-stderr-and-partial-trx-preserved')
    Save-GeneratedSdkDiagnostics "$output/absent-rebuild" $preserved
    $checks.Add('early-failure-with-no-rebuild-reports')
    Assert-GeneratedSdkInputs $SmokeDirectory $PackagePath $RuntimeReferenceAssetPath | Out-Null
    $checks.Add('complete-installed-tool-inputs')
    Reject 'missing-generated-source' {
        param($d)
        Remove-Item -LiteralPath (Get-ChildItem "$d/generated/Generated/R5" -Recurse -File -Filter '*.cs' | Select-Object -First 1).FullName
    } 'inventory/hash mismatch'
    Reject 'unexpected-generated-source' {
        param($d)
        [IO.File]::WriteAllText("$d/generated/Generated/R5/Unexpected.cs", '// extra')
    } 'inventory/hash mismatch'
    Reject 'changed-generated-source' {
        param($d)
        [IO.File]::AppendAllText((Get-ChildItem "$d/generated/Generated/R5" -Recurse -File -Filter '*.cs' | Select-Object -First 1).FullName, '// changed')
    } 'inventory/hash mismatch'
    Reject 'changed-generated-source-encoding' {
        param($d)
        $path = (Get-ChildItem "$d/generated/Generated/R5" -Recurse -File -Filter '*.cs' | Select-Object -First 1).FullName
        $content = [IO.File]::ReadAllText($path)
        [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($true))
    } 'inventory/hash mismatch'
    Reject 'failed-models-cli' {
        param($d)
        $s = Get-Content "$d/smoke-summary.json" -Raw | ConvertFrom-Json; $s.status = 'failed'
        WriteFixtureJson "$d/smoke-summary.json" $s
    } 'successful clean tool smoke'
    Reject 'different-installed-package' {
        param($d)
        $s = Get-Content "$d/smoke-summary.json" -Raw | ConvertFrom-Json; $s.packageSha256 = 'wrong'
        WriteFixtureJson "$d/smoke-summary.json" $s
    } 'package hash mismatch'
    Reject 'different-canonical-reference' {
        param($d)
        $path = "$d/generated/Generated/R5/model-generation-manifest.json"
        $m = Get-Content $path -Raw | ConvertFrom-Json; $m.compatibility.compilerReference.sha256 = 'wrong'
        WriteFixtureJson $path $m; RefreshHashes $d
    } 'provenance mismatch'
    Reject 'different-primitive-tool-provenance' {
        param($d)
        $path = "$d/generated/Generated/R5/Primitives/primitive-generation-manifest.json"
        $m = Get-Content $path -Raw | ConvertFrom-Json; $m.compatibility.tool.version = '0.0.0'
        WriteFixtureJson $path $m; RefreshHashes $d
    } 'provenance mismatch'
    Reject 'partial-model-generation' {
        param($d)
        $path = "$d/generated/Generated/R5/model-generation-manifest.json"
        $m = Get-Content $path -Raw | ConvertFrom-Json; $m.generationScope.mode = 'selected'
        WriteFixtureJson $path $m; RefreshHashes $d
    } 'full model generation'
    Reject 'different-fhir-package' {
        param($d)
        $path = "$d/generated/Generated/R5/model-generation-manifest.json"
        $m = Get-Content $path -Raw | ConvertFrom-Json; $m.package.sha256 = 'wrong'
        WriteFixtureJson $path $m; RefreshHashes $d
    } 'FHIR package provenance mismatch'
    Reject 'different-primitive-policy' {
        param($d)
        $path = "$d/generated/Generated/R5/model-generation-manifest.json"
        $m = Get-Content $path -Raw | ConvertFrom-Json; $m.primitivePolicy.sha256 = 'wrong'
        WriteFixtureJson $path $m; RefreshHashes $d
    } 'primitive policy provenance mismatch'
    $result.status = 'passed'
} catch {
    $result.status = 'failed'; $result.failure = $_.Exception.Message
    throw
} finally {
    $result.checks = $checks.ToArray()
    Write-KernelJson "$output/evidence.json" $result
}
Write-Output "K6 input harness: $($checks.Count) checks passed."
