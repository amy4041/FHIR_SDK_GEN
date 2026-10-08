#Requires -Version 7.2
[CmdletBinding()]
param([string] $OutputDirectory = 'artifacts/kernel-k3-harness')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
. (Join-Path $PSScriptRoot 'KernelConsumerRebuild.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path -LiteralPath $output)) { throw 'Use a fresh directory under artifacts/.' }
New-Item -ItemType Directory -Path $output | Out-Null
$checks = [Collections.Generic.List[string]]::new()
function Reject([string] $Name, [scriptblock] $Action, [string] $ExpectedMessage) {
    $message = $null
    try { & $Action | Out-Null } catch { $message = $_.Exception.Message }
    if (-not $message -or -not $message.Contains($ExpectedMessage)) { throw "Failure probe $Name did not reject as expected: $message" }
    $checks.Add($Name)
}
function WriteReport([string] $Directory, [string] $File, [string] $Assembly, [string] $Name = 'Probe.Passes', [string] $Outcome = 'Passed', [string] $Reason = '', [string] $CodeBase = '') {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    if (-not $CodeBase) { $CodeBase = "/fresh/output/$Assembly" }
    $escapedPath = [Security.SecurityElement]::Escape($CodeBase)
    $escapedReason = [Security.SecurityElement]::Escape($Reason)
    $passed = if ($Outcome -eq 'Passed') { 1 } else { 0 }
    $failed = if ($Outcome -eq 'Failed') { 1 } else { 0 }
    $text = @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions><UnitTest id="id1"><TestMethod codeBase="$escapedPath" /></UnitTest></TestDefinitions>
  <Results><UnitTestResult testId="id1" testName="$Name" outcome="$Outcome"><Output><ErrorInfo><Message>$escapedReason</Message></ErrorInfo></Output></UnitTestResult></Results>
  <ResultSummary><Counters total="1" passed="$passed" failed="$failed" /></ResultSummary>
</TestRun>
"@
    [IO.File]::WriteAllText("$Directory/$File.trx", $text, [Text.UTF8Encoding]::new($false))
}

$repository = "$output/repository"
foreach ($project in @('MyFhirSdk.csproj', 'Tests/Probe/Probe.csproj', 'Apps/BusinessConsumer/BusinessConsumer.csproj',
    'artifacts/ignored/Ignore.csproj', 'Tests/Probe/bin/Ignore.csproj', 'Tests/Probe/obj/Ignore.csproj', '.git/Ignore.csproj')) {
    $path = Join-Path $repository $project
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null
    [IO.File]::WriteAllText($path, '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../MyFhirSdk.csproj" /></ItemGroup></Project>')
}
$projects = @(Get-KernelConsumerProjects $repository)
if ($projects.Count -ne 3 -or $projects -cnotcontains 'Apps/BusinessConsumer/BusinessConsumer.csproj') { throw 'Repository-wide discovery missed a consumer or included generated projects.' }
$checks.Add('repository-wide-discovery-and-output-exclusions')
Reject 'unknown-business-consumer' { Assert-KernelConsumerProjectSet $projects @('MyFhirSdk.csproj', 'Tests/Probe/Probe.csproj') } 'Unclassified consumer project: Apps/BusinessConsumer'
Reject 'missing-known-project' { Assert-KernelConsumerProjectSet @('MyFhirSdk.csproj') @('MyFhirSdk.csproj', 'Tests/Probe/Probe.csproj') } 'Missing consumer project:'

$expected = @('MyFhirSdk.CodeGen.Tests.dll', 'MyFhirSdk.Client.Tests.dll')
$good = "$output/good"
WriteReport $good 'codegen' $expected[0] -CodeBase 'C:\fresh\output\MyFhirSdk.CodeGen.Tests.dll'
WriteReport $good 'client' $expected[1]
$result = Assert-KernelConsumerRegression $good $expected
if ($result.counts.passed -ne 2 -or $result.projects.Count -ne 2) { throw 'Healthy per-project reports were rejected.' }
$checks.Add('per-project-identity-and-windows-linux-paths')

WriteReport "$output/all-skipped" 'codegen' $expected[0] 'Probe.Skipped' 'NotExecuted' 'disabled'
WriteReport "$output/all-skipped" 'client' $expected[1]
Reject 'entire-codegen-project-skipped' { Assert-KernelConsumerRegression "$output/all-skipped" $expected } 'Unexpected skipped test:'

Copy-Item $good -Destination "$output/unexpected-skip" -Recurse
WriteReport "$output/unexpected-skip" 'client-duplicate' $expected[1]
Reject 'duplicate-project-report' { Assert-KernelConsumerRegression "$output/unexpected-skip" $expected } 'Duplicate test assembly report:'
WriteReport "$output/missing" 'codegen' $expected[0]
Reject 'missing-project-report' { Assert-KernelConsumerRegression "$output/missing" $expected } 'Missing test assembly report:'
WriteReport "$output/wrong-identity" 'codegen' $expected[0]
WriteReport "$output/wrong-identity" 'client' 'Unrelated.Tests.dll'
Reject 'unrelated-report-replaces-required-project' { Assert-KernelConsumerRegression "$output/wrong-identity" $expected } 'Unexpected test assembly:'
WriteReport "$output/failed" 'codegen' $expected[0] 'Probe.Fails' 'Failed'
WriteReport "$output/failed" 'client' $expected[1]
Reject 'failed-test' { Assert-KernelConsumerRegression "$output/failed" $expected } 'Unsuccessful test result:'

$external = "$output/external-smoke"
Copy-Item $good -Destination $external -Recurse
[xml] $client = Get-Content "$external/client.trx" -Raw
$skipped = $client.TestRun.Results.UnitTestResult.CloneNode($true)
$skipped.SetAttribute('testName', 'MyFhirSdk.Tests.Client.FhirClientIntegrationSmokeTests.PatientCrudSearchSmokeFlow')
$skipped.SetAttribute('outcome', 'NotExecuted')
$skipped.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']").InnerText = 'Set MYFHIRSDK_INTEGRATION_BASE_URL to run integration smoke tests.'
$client.TestRun.Results.AppendChild($skipped) | Out-Null
$client.TestRun.ResultSummary.Counters.SetAttribute('total', '2')
$client.Save("$external/client.trx")
$previousUrl = [Environment]::GetEnvironmentVariable('MYFHIRSDK_INTEGRATION_BASE_URL')
try {
    [Environment]::SetEnvironmentVariable('MYFHIRSDK_INTEGRATION_BASE_URL', $null)
    $result = Assert-KernelConsumerRegression $external $expected
    if ($result.counts.skipped -ne 1 -or $result.counts.passed -ne 2) { throw 'Known external smoke skip did not pass.' }
    $checks.Add('known-external-smoke-skip')
    WriteReport "$output/no-executed-tests" 'codegen' $expected[0]
    WriteReport "$output/no-executed-tests" 'client' $expected[1] 'MyFhirSdk.Tests.Client.FhirClientIntegrationSmokeTests.PatientCrudSearchSmokeFlow' 'NotExecuted' 'Set MYFHIRSDK_INTEGRATION_BASE_URL to run integration smoke tests.'
    Reject 'each-project-must-execute-tests' { Assert-KernelConsumerRegression "$output/no-executed-tests" $expected } 'No tests executed successfully:'
    [Environment]::SetEnvironmentVariable('MYFHIRSDK_INTEGRATION_BASE_URL', 'https://example.invalid')
    Reject 'configured-external-smoke-must-run' { Assert-KernelConsumerRegression $external $expected } 'Unexpected skipped test:'
} finally { [Environment]::SetEnvironmentVariable('MYFHIRSDK_INTEGRATION_BASE_URL', $previousUrl) }

# The only permitted skipped case cannot be used as a blanket skip allowance.
WriteReport "$output/bad-client-skip" 'codegen' $expected[0]
WriteReport "$output/bad-client-skip" 'client' $expected[1] 'Probe.Skipped' 'NotExecuted' 'disabled'
Reject 'unexpected-client-skip' { Assert-KernelConsumerRegression "$output/bad-client-skip" $expected } 'Unexpected skipped test:'
Copy-Item $good -Destination "$output/counters" -Recurse
[xml] $counterReport = Get-Content "$output/counters/codegen.trx" -Raw
$counterReport.TestRun.ResultSummary.Counters.SetAttribute('passed', '99')
$counterReport.Save("$output/counters/codegen.trx")
Reject 'counter-result-mismatch' { Assert-KernelConsumerRegression "$output/counters" $expected } 'Regression counter mismatch:'

Write-KernelJson "$output/summary.json" ([ordered]@{ status = 'passed'; checks = $checks.ToArray() })
Write-Output "K3 failure-mode harness: $($checks.Count) checks passed."
exit 0
