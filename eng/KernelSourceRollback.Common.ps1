function Get-KernelCurrentSourceFingerprint {
    param([Parameter(Mandatory)] [string] $RepositoryRoot)
    $listed = @(& git -C $RepositoryRoot -c core.quotepath=false ls-files --cached --others --exclude-standard --deduplicate -z)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fingerprint current source.' }
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($relative in ($listed -join "`n").Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($relative -match '(^|/)(artifacts|bin|obj|\.git|\.codex|\.agents|\.aws|\.vs|\.idea)(/|$)') { continue }
        $path = Join-Path $RepositoryRoot $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source links are unsupported: $relative" }
        $hash = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
        $lines.Add("$relative|$hash")
    }
    if ($lines.Count -eq 0) { throw 'Current source fingerprint is empty.' }
    $fileCount = $lines.Count
    # A changed integration endpoint must not reuse an earlier skipped/passed smoke result.
    $endpointHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([string] $env:MYFHIRSDK_INTEGRATION_BASE_URL))).ToLowerInvariant()
    $lines.Add("integration-endpoint|$endpointHash")
    [string[]] $ordered = $lines.ToArray()
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    $digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($ordered -join "`n") + "`n"))).ToLowerInvariant()
    [ordered]@{ schemaVersion = 1; algorithm = 'sha256-path-and-bytes-v1'; fileCount = $fileCount; sha256 = $digest }
}

function Test-KernelRegressionFingerprint {
    param($BaselineEvidence, $CurrentFingerprint)
    $property = $BaselineEvidence.PSObject.Properties['currentSourceFingerprint']
    if ($null -eq $property -or $null -eq $property.Value) { return $false }
    $saved = $property.Value
    return $saved.schemaVersion -eq $CurrentFingerprint.schemaVersion -and
        $saved.algorithm -ceq $CurrentFingerprint.algorithm -and $saved.fileCount -eq $CurrentFingerprint.fileCount -and
        $saved.sha256 -ceq $CurrentFingerprint.sha256
}

function Get-KernelRegressionAssemblies {
    @('MyFhirSdk.Architecture.Tests.dll', 'MyFhirSdk.CodeGen.Tests.dll', 'MyFhirSdk.Client.Tests.dll',
      'MyFhirSdk.Parser.Json.Tests.dll', 'MyFhirSdk.Serialization.Json.Tests.dll',
      'MyFhirSdk.Validation.Tests.dll', 'MyFhirSdk.ImplementationGuides.TwCore.Tests.dll')
}

function Assert-KernelRollbackConsumer {
    param([string] $DeploymentDirectory, [string] $ExpectedHash)
    if ($ExpectedHash -notmatch '^[0-9a-f]{64}$' -or
        (Get-FileHash -LiteralPath (Join-Path $DeploymentDirectory 'Consumer.dll')).Hash.ToLowerInvariant() -cne $ExpectedHash) {
        throw 'Rollback consumer binary hash mismatch.'
    }
}

function Assert-KernelRollbackConsumerOutput {
    param([string] $LogPath)
    $expected = 'Consumer passed: primitive access, model/base assignment, JSON round-trip.'
    $content = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $LogPath).Path)
    if ($content.Trim() -cne $expected) { throw 'Rollback consumer behavior output mismatch.' }
}
