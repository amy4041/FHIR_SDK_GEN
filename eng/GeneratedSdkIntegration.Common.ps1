# Shared input validation for the K6 SDK rebuild and its rejection-case harness.
function Get-GeneratedSdkSourceFiles {
    param([Parameter(Mandatory)] [string] $RepositoryRoot)
    $listed = @(& git -C $RepositoryRoot -c core.quotepath=false ls-files --cached --others --exclude-standard --deduplicate -z)
    if ($LASTEXITCODE -ne 0 -or $listed.Count -eq 0) { throw 'Cannot enumerate SDK source files.' }
    foreach ($relative in ($listed -join "`n").Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($relative -match '(^|/)(bin|obj|artifacts|\.git|\.codex|\.agents|\.aws|\.vs|\.idea)(/|$)' -or
            $relative.StartsWith('Generated/R5/', [StringComparison]::Ordinal)) { continue }
        # A tracked file deleted in the working tree is also absent from a normal build.
        if (Test-Path -LiteralPath (Join-Path $RepositoryRoot $relative) -PathType Leaf) { $relative }
    }
}

function Save-GeneratedSdkDiagnostics {
    param([Parameter(Mandatory)] [string] $RebuildDirectory, [Parameter(Mandatory)] [string] $OutputDirectory)
    if (-not (Test-Path -LiteralPath $RebuildDirectory)) { return }
    # These reports contain the isolated process stdout/stderr, including unexpected negative-test failures.
    foreach ($name in @('evidence.json', 'consumer-inventory.json', 'current-consumer-run.json',
        'deployment-run.json', 'missing-runtime-run.json', 'test-results')) {
        $source = Join-Path $RebuildDirectory $name
        $destination = Join-Path $OutputDirectory $(if ($name -eq 'evidence.json') { 'consumer-rebuild-evidence.json' } else { $name })
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
        }
    }
}

function Get-GeneratedSdkHashes {
    param([Parameter(Mandatory)] [string] $Root)
    $result = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Generated inputs must be regular files.' }
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $logical = if ($relative.StartsWith('Primitives/', [StringComparison]::Ordinal)) {
            'primitive|' + $relative.Substring('Primitives/'.Length)
        } else { 'model|' + $relative }
        # The generator already writes UTF-8/LF. Preserve exact bytes consumed by the SDK.
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $result.Add("$logical|$hash")
    }
    [string[]] $lines = $result.ToArray()
    [Array]::Sort($lines, [StringComparer]::Ordinal)
    return $lines
}

function Assert-GeneratedSdkInputs {
    param(
        [Parameter(Mandatory)] [string] $SmokeDirectory,
        [Parameter(Mandatory)] [string] $PackagePath,
        [Parameter(Mandatory)] [string] $RuntimeReferenceAssetPath
    )
    $summary = Get-Content -LiteralPath (Join-Path $SmokeDirectory 'smoke-summary.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($summary.status -cne 'passed' -or $summary.isolatedInputs -ne $true -or
        $summary.exportedGeneratedRoot -cne 'generated/Generated/R5' -or
        $summary.modelSourceCount -ne 831 -or $summary.modelArtifactCount -ne 832 -or $summary.primitiveArtifactCount -ne 22) {
        throw 'A successful clean tool smoke with a complete exported source set is required.'
    }
    $packageHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($packageHash -cne $summary.packageSha256) { throw 'Installed tool package hash mismatch.' }
    $generated = Join-Path $SmokeDirectory $summary.exportedGeneratedRoot
    $actual = @(Get-GeneratedSdkHashes $generated)
    $expected = @(Get-Content -LiteralPath (Join-Path $SmokeDirectory 'artifact-hashes.txt') -Encoding utf8)
    if ($actual.Count -ne 854 -or ($actual -join "`n") -cne ($expected -join "`n")) {
        throw 'Exported generated input inventory/hash mismatch.'
    }
    $sources = @(Get-ChildItem -LiteralPath $generated -Recurse -File -Filter '*.cs')
    if ($sources.Count -ne 852) { throw 'Expected 852 generated C# sources.' }
    $model = Get-Content -LiteralPath (Join-Path $generated 'model-generation-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
    $primitive = Get-Content -LiteralPath (Join-Path $generated 'Primitives/primitive-generation-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
    $referenceHash = (Get-FileHash -LiteralPath $RuntimeReferenceAssetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    foreach ($manifest in @($model, $primitive)) {
        if ($manifest.schemaVersion -ne 3 -or $manifest.compatibility.tool.packageId -cne $summary.packageId -or
            $manifest.compatibility.tool.version -cne $summary.toolVersion -or
            $manifest.compatibility.targetFramework -cne $summary.targetFramework -or
            $manifest.compatibility.compilerReference.sha256 -cne $referenceHash) {
            throw 'Generated manifest/installed tool/canonical reference provenance mismatch.'
        }
    }
    if ($model.primitivePolicy.sha256 -cne $summary.primitivePolicySha256 -or
        $primitive.policyVersion -cne $model.primitivePolicy.version) { throw 'Generated primitive policy provenance mismatch.' }
    if ($model.package.sha256 -cne $summary.fhirPackageSha256 -or
        $primitive.fhirPackageId -cne $model.package.id -or $primitive.fhirPackageVersion -cne $model.package.version) {
        throw 'Generated FHIR package provenance mismatch.'
    }
    if ($model.generationScope.mode -cne 'full' -or @($model.generationScope.selectedCanonicals).Count -ne 0) { throw 'A full model generation is required.' }
    return [ordered]@{ summary = $summary; generatedRoot = $generated; hashes = $actual; referenceSha256 = $referenceHash }
}
