#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $WindowsPackageInventory,
    [Parameter(Mandatory)] [string] $LinuxPackageInventory,
    [Parameter(Mandatory)] [string] $WindowsArtifactHashes,
    [Parameter(Mandatory)] [string] $LinuxArtifactHashes,
    [Parameter(Mandatory)] [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-IdenticalFile {
    param([string] $First, [string] $Second, [string] $Label)

    $firstBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $First).Path)
    $secondBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Second).Path)
    $firstHash = [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($firstBytes))
    $secondHash = [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($secondBytes))
    if ($firstHash -cne $secondHash) {
        $firstLines = [System.IO.File]::ReadAllLines(
            (Resolve-Path -LiteralPath $First).Path)
        $secondLines = [System.IO.File]::ReadAllLines(
            (Resolve-Path -LiteralPath $Second).Path)
        $differences = [System.Collections.Generic.List[string]]::new()
        $lineCount = [System.Math]::Max($firstLines.Length, $secondLines.Length)
        for ($index = 0; $index -lt $lineCount -and $differences.Count -lt 10; $index++) {
            $windowsLine = if ($index -lt $firstLines.Length) {
                $firstLines[$index]
            }
            else {
                '<missing>'
            }
            $linuxLine = if ($index -lt $secondLines.Length) {
                $secondLines[$index]
            }
            else {
                '<missing>'
            }
            if ($windowsLine -cne $linuxLine) {
                $differences.Add(
                    "line $($index + 1): windows='$windowsLine'; linux='$linuxLine'")
            }
        }
        throw "$Label differs between Windows and Linux. $($differences -join ' | ')"
    }
}

Assert-IdenticalFile $WindowsPackageInventory $LinuxPackageInventory 'Normalized package inventory'
Assert-IdenticalFile $WindowsArtifactHashes $LinuxArtifactHashes 'Generated artifact hashes'

$result = [ordered]@{
    schemaVersion = 1
    status = 'passed'
    packageInventory = 'byte-identical'
    generatedArtifacts = 'byte-identical'
}
$parent = [System.IO.Path]::GetDirectoryName(
    [System.IO.Path]::GetFullPath($OutputPath))
[void] [System.IO.Directory]::CreateDirectory($parent)
[System.IO.File]::WriteAllText(
    $OutputPath,
    (($result | ConvertTo-Json) + "`n"),
    [System.Text.UTF8Encoding]::new($false))
