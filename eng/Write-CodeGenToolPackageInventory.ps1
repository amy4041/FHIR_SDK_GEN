#Requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [string] $ExpectedTargetFramework,
    [Parameter(Mandatory)] [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NormalizedTextHash {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)

    $reader = [System.IO.StreamReader]::new(
        $Entry.Open(),
        [System.Text.UTF8Encoding]::new($false, $true))
    try {
        $content = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
    $normalizedBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(
        $content.Replace("`r`n", "`n").Replace("`r", "`n"))
    return [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($normalizedBytes)
    ).ToLowerInvariant()
}

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
        if ($normalizedPath -ceq 'tools/<tfm>/any/MyFhirSdk.CodeGen.dll' -or
            $normalizedPath -ceq 'tools/<tfm>/any/MyFhirSdk.CodeGen.pdb') {
            # Portable PDB document checksums include SDK-generated sources whose
            # line endings follow the build host. The PE debug identity therefore
            # also differs even when the compiled program is semantically equal.
            $lines.Add("$normalizedPath|<platform-build-output>")
            continue
        }
        if ($normalizedPath -ceq 'MyFhirSdk.CodeGen.Tool.nuspec' -or
            $normalizedPath -ceq 'tools/<tfm>/any/DotnetToolSettings.xml' -or
            $normalizedPath -ceq 'tools/<tfm>/any/MyFhirSdk.CodeGen.deps.json' -or
            $normalizedPath -ceq 'tools/<tfm>/any/MyFhirSdk.CodeGen.runtimeconfig.json') {
            $lines.Add("$normalizedPath|$(Get-NormalizedTextHash $entry)")
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
