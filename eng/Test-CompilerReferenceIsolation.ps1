#Requires -Version 7.2
[CmdletBinding()]
param([string] $OutputPath = 'artifacts/kernel-k1/compiler-isolation-summary.json')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml] $props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw -Encoding utf8
$tfm = [string] $props.Project.PropertyGroup.MyFhirSdkTargetFramework
$descriptor = Get-Content (Join-Path $root 'CodeGen/Policy/runtime-contract.json') -Raw -Encoding utf8 | ConvertFrom-Json
$reference = Join-Path $root "artifacts/compiler-contract/Release/$tfm/MyFhirSdk.dll"
$consumer = 'Tests/Architecture/MyFhirSdk.Architecture.Tests.csproj'

function Invoke-DotNet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $Arguments" }
}

function Get-Snapshot {
    $hashes = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($directory in @("obj/Release/$tfm", "bin/Release/$tfm", "Runtime/obj/Release/$tfm", "Runtime/bin/Release/$tfm")) {
        foreach ($file in Get-ChildItem (Join-Path $root $directory) -Recurse -File) {
            $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            $hashes.Add($relative, (Get-FileHash $file.FullName -Algorithm SHA256).Hash)
        }
    }
    return ,$hashes
}

function Assert-Unchanged($Before, $After) {
    if ($Before.Count -ne $After.Count) { throw 'Compiler contract build changed the normal SDK output inventory.' }
    foreach ($path in $Before.Keys) {
        if (-not $After.ContainsKey($path) -or $After[$path] -cne $Before[$path]) {
            throw "Compiler contract build changed or removed a normal SDK output: $path"
        }
    }
}

Push-Location $root
try {
    # Requires the solution restore, as in CI. Start with a real built consumer.
    Invoke-DotNet @('msbuild', 'MyFhirSdk.csproj', '/t:GetCompilerReferenceAsset', '/p:Configuration=Release', '/v:quiet')
    Invoke-DotNet @('build', $consumer, '-c', 'Release', '--no-restore', '--verbosity', 'quiet', "-p:RuntimeReferenceAssetPath=$reference")
    $before = Get-Snapshot
    $normalReference = "obj/Release/$tfm/ref/MyFhirSdk.dll"
    if (-not $before.ContainsKey($normalReference)) { throw 'Normal SDK reference was not produced.' }

    # Exercise both a fresh contract compile and an incremental contract build.
    # Only the isolated contract cache is rebuilt; normal outputs must survive.
    Invoke-DotNet @('msbuild', 'MyFhirSdk.csproj', '/t:Rebuild;GetLegacyCompilerReferenceAsset', '/p:Configuration=Release', '/p:MyFhirSdkCompilerContract=true', '/v:quiet')
    Assert-Unchanged $before (Get-Snapshot)
    Invoke-DotNet @('msbuild', 'MyFhirSdk.csproj', '/t:GetCompilerReferenceAsset', '/p:Configuration=Release', '/v:quiet')
    Assert-Unchanged $before (Get-Snapshot)
    $hash = (Get-FileHash $reference -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -cne [string] $descriptor.compilerReference.sha256) { throw "Frozen compiler reference hash drifted: $hash" }

    Invoke-DotNet @('build', $consumer, '-c', 'Release', '--no-restore', '-p:BuildProjectReferences=false', '--verbosity', 'quiet')
    Assert-Unchanged $before (Get-Snapshot)

    $output = [IO.Path]::GetFullPath($OutputPath, $root)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
    $result = [ordered]@{
        status = 'passed'
        targetFramework = $tfm
        normalSdkOutputCount = $before.Count
        normalSdkOutputsUnchanged = $true
        compilerReferenceSha256 = $hash
        consumerBuildWithoutProjectReferences = 'passed'
    }
    [IO.File]::WriteAllText($output, (($result | ConvertTo-Json) + "`n"), [Text.UTF8Encoding]::new($false))
}
finally {
    Pop-Location
}
