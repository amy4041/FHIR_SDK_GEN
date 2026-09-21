#Requires -Version 7.2

[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/primitive-tgz-p0/rebuilt'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$output = [System.IO.Path]::GetFullPath($OutputDirectory, $repositoryRoot)
$comparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }
if (-not $output.StartsWith($artifactRoot + [System.IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'Baseline export must be under repository artifacts/; approved baselines are never overwritten.'
}

$previousExport = [Environment]::GetEnvironmentVariable('MYFHIRSDK_PRIMITIVE_P0_EXPORT')
try {
    [Environment]::SetEnvironmentVariable('MYFHIRSDK_PRIMITIVE_P0_EXPORT', $output)
    dotnet test (Join-Path $repositoryRoot 'Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj') `
        -c Release --no-restore --filter FullyQualifiedName~PrimitiveTgzInputBaselineTests
    if ($LASTEXITCODE -ne 0) {
        throw "Primitive baseline verification failed. Any rebuilt evidence is at '$output'; review differences without automatically accepting them."
    }
    Write-Output "Primitive generation baseline verified and exported to $output"
}
finally {
    [Environment]::SetEnvironmentVariable('MYFHIRSDK_PRIMITIVE_P0_EXPORT', $previousExport)
}
