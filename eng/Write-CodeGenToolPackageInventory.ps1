#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [string] $ExpectedTargetFramework,
    [Parameter(Mandatory)] [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$package = (Resolve-Path -LiteralPath $PackagePath).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $descriptorPath = "tools/$ExpectedTargetFramework/any/Contracts/runtime-contract.json"
    $descriptorEntry = $archive.GetEntry($descriptorPath)
    if ($null -eq $descriptorEntry) {
        throw "Package is missing $descriptorPath."
    }
    $reader = [System.IO.StreamReader]::new(
        $descriptorEntry.Open(),
        [System.Text.UTF8Encoding]::new($false, $true))
    try {
        $descriptor = $reader.ReadToEnd() | ConvertFrom-Json
    }
    finally {
        $reader.Dispose()
    }
    if ([string] $descriptor.targetFramework -cne $ExpectedTargetFramework -or
        [string] $descriptor.compilerReference.targetFramework -cne $ExpectedTargetFramework) {
        throw 'Package layout, descriptor targetFramework, and compilerReference targetFramework must match.'
    }

    [System.Collections.Generic.List[string]] $lines = @()
    foreach ($entry in $archive.Entries) {
        $path = $entry.FullName
        if ($path.StartsWith(
                'package/services/metadata/core-properties/',
                [System.StringComparison]::Ordinal)) {
            $lines.Add('package/services/metadata/core-properties/<generated>.psmdcp|<container-metadata>')
            continue
        }
        $normalizedPath = $path.Replace(
            "tools/$ExpectedTargetFramework/any/",
            'tools/<tfm>/any/',
            [System.StringComparison]::Ordinal)
        $normalizedPath = $normalizedPath.Replace(
            "/RuntimeReferences/$ExpectedTargetFramework/",
            '/RuntimeReferences/<tfm>/',
            [System.StringComparison]::Ordinal)
        if ($path -ceq '[Content_Types].xml' -or $path -ceq '_rels/.rels') {
            $lines.Add("$normalizedPath|<container-metadata>")
            continue
        }
        $stream = $entry.Open()
        try {
            $hash = [System.Security.Cryptography.SHA256]::HashData($stream)
        }
        finally {
            $stream.Dispose()
        }
        $lines.Add("$normalizedPath|$([System.Convert]::ToHexString($hash).ToLowerInvariant())")
    }

    [string[]] $ordered = $lines.ToArray()
    [System.Array]::Sort($ordered, [System.StringComparer]::Ordinal)
    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputPath))
    [void] [System.IO.Directory]::CreateDirectory($parent)
    [System.IO.File]::WriteAllText(
        $OutputPath,
        (($ordered -join "`n") + "`n"),
        [System.Text.UTF8Encoding]::new($false))
}
finally {
    $archive.Dispose()
}
