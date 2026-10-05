# Shared by the K0 runner and its regression tests. No production build defaults change.
function Write-KernelJson {
    param([string] $Path, [System.Collections.IDictionary] $Value)
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n", [Text.UTF8Encoding]::new($false))
}

function Copy-KernelBaselineFixture {
    param([string] $RepositoryRoot, $Fixture, [string] $Destination)
    $names = @('Consumer.csproj', 'Program.cs')
    if (@($Fixture.files.PSObject.Properties).Count -ne $names.Count) { throw 'Unexpected baseline fixture file set.' }
    $manifest = ''
    foreach ($name in $names) {
        $expected = [string] $Fixture.files.$name
        if ($expected -notmatch '^[0-9a-f]{64}$') { throw "Invalid fixture hash: $name" }
        $manifest += "$expected  $name`n"
        $actual = (Get-FileHash -LiteralPath (Join-Path $RepositoryRoot "$($Fixture.directory)/$name") -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne $expected) { throw "Frozen baseline fixture drift: $name" }
    }
    $revision = 'sha256:' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($manifest))).ToLowerInvariant()
    if ($revision -cne $Fixture.revision) { throw 'Frozen fixture content revision mismatch.' }
    New-Item -ItemType Directory -Path $Destination | Out-Null
    foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $RepositoryRoot "$($Fixture.directory)/$name") -Destination $Destination }
}

function New-KernelEvidence {
    param([switch] $RunRegressionAndSmoke)
    $gates = [ordered]@{}
    foreach ($name in @('baselineBuild', 'oldConsumer', 'currentConsumer', 'apiInventory', 'sourceInventory', 'regression', 'primitiveEquivalence', 'toolSmoke', 'inventoryComparison')) {
        $gates[$name] = if (-not $RunRegressionAndSmoke -and $name -in @('regression', 'primitiveEquivalence', 'toolSmoke')) { 'skipped' } else { 'pending' }
    }
    [ordered]@{ schemaVersion = 1; status = 'running'; activeGate = $null; gates = $gates; failure = $null }
}

function Start-KernelGate {
    param([System.Collections.IDictionary] $Evidence, [string] $Name)
    if ($Evidence.activeGate -or -not $Evidence.gates.Contains($Name) -or $Evidence.gates[$Name] -cne 'pending') { throw "Invalid gate transition: $Name" }
    $Evidence.activeGate = $Name
    $Evidence.gates[$Name] = 'running'
}

function Complete-KernelGate {
    param([System.Collections.IDictionary] $Evidence)
    if (-not $Evidence.activeGate) { throw 'No active gate.' }
    $Evidence.gates[$Evidence.activeGate] = 'passed'
    $Evidence.activeGate = $null
}

function Invoke-KernelEvidenceRun {
    param([System.Collections.IDictionary] $Evidence, [string] $OutputPath, [scriptblock] $Action)
    Write-KernelJson $OutputPath $Evidence
    try {
        & $Action $Evidence
        if (@($Evidence.gates.Values | Where-Object { $_ -notin @('passed', 'skipped') }).Count -ne 0) { throw 'Required gates did not complete.' }
        $Evidence.status = if (@($Evidence.gates.Values | Where-Object { $_ -eq 'skipped' }).Count -gt 0) { 'partial' } else { 'passed' }
    }
    catch {
        $Evidence.status = 'failed'
        $Evidence.failure = [ordered]@{ gate = $Evidence.activeGate; message = $_.Exception.Message }
        if ($Evidence.activeGate) { $Evidence.gates[$Evidence.activeGate] = 'failed' }
        throw
    }
    finally { Write-KernelJson $OutputPath $Evidence }
}

function Assert-KernelInventories {
    param([string] $ApprovedDirectory, [string] $ActualDirectory)
    foreach ($name in @('public-api.txt', 'source-inventory.txt', 'package-inventory.txt')) {
        if (-not (Test-Path "$ApprovedDirectory/$name")) { throw "Missing committed K0 inventory: $name" }
        if ([IO.File]::ReadAllText("$ApprovedDirectory/$name").Replace("`r`n", "`n") -cne [IO.File]::ReadAllText("$ActualDirectory/$name").Replace("`r`n", "`n")) { throw "Baseline drift: $name" }
    }
}

function Get-KernelBaselineBuildProperties {
    param([Parameter(Mandatory)] [string] $SourceRevision)
    if ($SourceRevision -notmatch '^[0-9a-f]{40}$') { throw 'Expected immutable full source revision.' }
    # Exported sources are nested under a different Git checkout. Never discover its HEAD.
    @(
        '/p:EnableSourceControlManagerQueries=false'
        '/p:EnableSourceLink=false'
        "/p:SourceRevisionId=$SourceRevision"
        "/p:RepositoryCommit=$SourceRevision"
        '/p:RepositoryBranch='
        '/p:SourceBranchName='
    )
}

function Assert-KernelPackageProvenance {
    param([Parameter(Mandatory)] [string] $PackagePath, [Parameter(Mandatory)] [string] $SourceRevision)
    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $archive.GetEntry('MyFhirSdk.CodeGen.Tool.nuspec')
        if ($null -eq $entry) { throw 'Package nuspec is missing.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $repository = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='repository']")
        if ($null -eq $repository -or $repository.GetAttribute('commit') -cne $SourceRevision -or $repository.GetAttribute('branch') -ne '') {
            throw 'Package repository provenance does not match the detached baseline revision.'
        }
    } finally { $archive.Dispose() }
}
