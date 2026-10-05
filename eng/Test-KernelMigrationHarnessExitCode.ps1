#Requires -Version 7.2
[CmdletBinding()]
param([string] $OutputDirectory = 'artifacts/kernel-k0-harness-tests')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path $output)) {
    throw 'Use a fresh directory under artifacts/.'
}
$shellDirectory = $output + '-shell'
if (Test-Path $shellDirectory) { throw 'Shell test staging must be fresh.' }
New-Item -ItemType Directory -Path $shellDirectory | Out-Null
$wrapper = Join-Path $shellDirectory 'actions-wrapper.ps1'
# Reproduce GitHub Actions' built-in pwsh prefix and suffix. A direct pwsh -File
# invocation of the harness alone does not propagate stale native exit codes.
$wrapperText = @'
param([string] $HarnessPath, [string] $OutputDirectory)
$ErrorActionPreference = 'Stop'
& $HarnessPath -OutputDirectory $OutputDirectory
if ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit $LASTEXITCODE }
'@
[IO.File]::WriteAllText($wrapper, $wrapperText.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$harness = Join-Path $PSScriptRoot 'Test-KernelMigrationHarness.ps1'
& $pwsh -NoProfile -NonInteractive -File $wrapper -HarnessPath $harness -OutputDirectory $output
$successExitCode = $LASTEXITCODE
if ($successExitCode -ne 0) { throw "Passing harness returned $successExitCode through the Actions wrapper." }
$summary = Get-Content "$output/summary.json" -Raw | ConvertFrom-Json
if ($summary.status -cne 'passed') { throw 'Harness did not finish all regression checks.' }

# Reusing the output is a real, unhandled harness precondition failure. It must
# still fail the step; the success exit must never run in a catch/finally block.
$failureLog = Join-Path $shellDirectory 'unexpected-failure.log'
& $pwsh -NoProfile -NonInteractive -File $wrapper -HarnessPath $harness -OutputDirectory $output *> $failureLog
$failureExitCode = $LASTEXITCODE
if ($failureExitCode -eq 0) { throw 'Unexpected harness failure was masked.' }
if (-not ([IO.File]::ReadAllText($failureLog).Contains('Use a fresh directory under artifacts/'))) {
    throw 'Failure probe did not reach the expected harness precondition.'
}
Write-KernelJson "$output/shell-exit-summary.json" ([ordered]@{
    status = 'passed'; actionsWrapperSuccessExitCode = $successExitCode
    unexpectedFailureExitCode = $failureExitCode
})
Write-Output 'K0 harness Actions exit-code regression tests passed.'
exit 0
