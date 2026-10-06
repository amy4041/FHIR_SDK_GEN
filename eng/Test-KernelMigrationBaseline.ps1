#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/kernel-k0',
    [switch] $RunRegressionAndSmoke
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$artifactRoot = Join-Path $root 'artifacts'
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'Output must be under artifacts/.' }
if (Test-Path $output) { throw 'Use a fresh output directory to prove isolated reconstruction.' }
New-Item -ItemType Directory -Path $output | Out-Null
function Run([string] $Command, [string[]] $Arguments) {
    if ($Command -eq 'dotnet') { Invoke-KernelDotNet -Arguments $Arguments; return }
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE): $Arguments" }
}
function Hash([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function WriteText([string] $Path, [string] $Content) {
    [IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n") + "`n", [Text.UTF8Encoding]::new($false))
}
$result = New-KernelEvidence -RunRegressionAndSmoke:$RunRegressionAndSmoke
$result.buildUiLanguage = Get-KernelBuildUiLanguage
Invoke-KernelEvidenceRun -Evidence $result -OutputPath "$output/evidence.json" -Action {
    param($result)
    Start-KernelGate $result 'baselineBuild'
    $pin = Get-Content (Join-Path $PSScriptRoot 'kernel-migration-baseline.json') -Raw | ConvertFrom-Json
    $baselineBuildProperties = @(Get-KernelBaselineBuildProperties $pin.sourceRevision)
    if ($pin.sourceRevision -notmatch '^[0-9a-f]{40}$') { throw 'Expected immutable full source revision.' }
    $source = Join-Path $output 'baseline-source'
    New-Item -ItemType Directory -Path $source | Out-Null
    Export-KernelBaselineSource $root $pin.sourceRevision "$output/source.tar"
    Run tar @('-xf', "$output/source.tar", '-C', $source)
    [xml] $props = Get-Content "$source/Directory.Build.props" -Raw
    $tfm = [string] $props.Project.PropertyGroup.MyFhirSdkTargetFramework
    Push-Location $source
    try {
        $sdkVersion = (& dotnet --version).Trim()
        if ($LASTEXITCODE -ne 0 -or $sdkVersion -cne $pin.sdkVersion) { throw 'Pinned SDK version mismatch.' }
        Run dotnet (@('msbuild', 'eng/MyFhirSdk.CodeGen.Build.proj', '/t:Restore') + $baselineBuildProperties)
        Run dotnet (@('msbuild', 'eng/MyFhirSdk.CodeGen.Build.proj', '/t:Pack', '/p:Configuration=Release', "/p:PackageOutputPath=$output/package") + $baselineBuildProperties)
    } finally { Pop-Location }
    $sdk = "$source/bin/Release/$tfm/MyFhirSdk.dll"
    $reference = "$source/CodeGen/bin/Release/$tfm/Assets/RuntimeReferences/$tfm/MyFhirSdk.dll"
    $descriptor = "$source/CodeGen/Policy/runtime-contract.json"
    Assert-KernelSdkBaseline -AssemblyPath $sdk -Pin $pin -Evidence $result
    if ((Hash $descriptor) -cne $pin.descriptorSha256 -or (Hash $reference) -cne $pin.compilerReferenceSha256) { throw 'Pinned descriptor/reference hash mismatch.' }
    $packages = @(Get-ChildItem "$output/package" -Filter *.nupkg)
    if ($packages.Count -ne 1) { throw 'Expected exactly one canonical package.' }
    Assert-KernelPackageProvenance $packages[0].FullName $pin.sourceRevision
    & "$root/eng/Write-CodeGenToolPackageInventory.ps1" -PackagePath $packages[0].FullName -ExpectedTargetFramework $tfm -OutputPath "$output/package-inventory.txt"

    Complete-KernelGate $result
    Start-KernelGate $result 'oldConsumer'
    # Content-addressed fixture source stays independent of the current-source consumer.
    $fixtureCopy = "$output/fixture"
    Copy-KernelBaselineFixture $root $pin.fixture $fixtureCopy
    Run dotnet (@('build', "$fixtureCopy/Consumer.csproj", '-c', 'Release', "-p:MyFhirSdkTargetFramework=$tfm", "-p:SdkAssemblyPath=$sdk", "-p:PathMap=$fixtureCopy=/_/kernel-consumer", '/p:IncludeSourceRevisionInInformationalVersion=false', '-o', "$output/old-consumer") + $baselineBuildProperties)
    $consumerHash = Hash "$output/old-consumer/Consumer.dll"
    Run dotnet @("$output/old-consumer/Consumer.dll")
    Complete-KernelGate $result
    Start-KernelGate $result 'currentConsumer'
    $currentFixture = "$output/current-fixture"
    New-Item -ItemType Directory -Path $currentFixture | Out-Null
    Copy-Item "$root/Tests/KernelMigration/Consumer/Consumer.csproj", "$root/Tests/KernelMigration/Consumer/Program.cs" $currentFixture

    Run dotnet @('build', "$root/MyFhirSdk.csproj", '-c', 'Release')
    $currentSdk = "$root/bin/Release/$tfm/MyFhirSdk.dll"
    Run dotnet @('build', "$currentFixture/Consumer.csproj", '-c', 'Release', "-p:MyFhirSdkTargetFramework=$tfm", "-p:SdkAssemblyPath=$currentSdk", '-o', "$output/current-consumer")
    Run dotnet @("$output/current-consumer/Consumer.dll")
    if ((Hash "$output/old-consumer/Consumer.dll") -cne $consumerHash) { throw 'Old consumer binary was modified.' }

    Complete-KernelGate $result
    Start-KernelGate $result 'apiInventory'
    $inventoryProject = "$root/Tests/KernelMigration/Inventory/Inventory.csproj"
    Run dotnet @('build', $inventoryProject, '-c', 'Release', '-o', "$output/inventory-tool")
    Run dotnet @("$output/inventory-tool/Inventory.dll", $sdk, "$output/public-api.txt")
    Run dotnet @("$output/inventory-tool/Inventory.dll", $sdk, "$output/public-api-repeat.txt")
    if ((Hash "$output/public-api.txt") -cne (Hash "$output/public-api-repeat.txt")) { throw 'Non-deterministic inventory.' }
    $inventory = Get-Content "$output/public-api.txt"
    $r5Count = @($inventory | Where-Object { $_ -match '^TYPE .*\]MyFhirSdk\.(Types|Resources)\.' -or $_ -match '^TYPE .*\]MyFhirSdk.Core.(BackboneElement|BackboneType|Base|DataType|DomainResource|Element|Extension|FhirObject|IFhirExtensionValue|Meta|Narrative|Resource) ' }).Count
    $contract = Get-Content $descriptor -Raw | ConvertFrom-Json
    if ($contract.symbols.Count -ne $pin.expectedRuntimeSymbols -or $r5Count -ne $pin.expectedR5SurfaceTypes) { throw "Inventory count mismatch: R5=$r5Count. Investigate; do not trim inventory." }
    foreach ($symbol in $contract.symbols) {
        $pattern = '^TYPE \[MyFhirSdk\]' + [regex]::Escape([string] $symbol.clrType) + '( |<)'
        if (@($inventory | Where-Object { $_ -match $pattern }).Count -ne 1) { throw "Descriptor symbol missing or duplicated: $($symbol.clrType)" }
    }
    Complete-KernelGate $result
    Start-KernelGate $result 'sourceInventory'
    $modelFiles = @(Get-ChildItem "$source/Generated/R5/Types", "$source/Generated/R5/Resources", "$source/Generated/R5/ModelMetadata" -Recurse -Filter *.cs)
    $primitiveFiles = @(Get-ChildItem "$source/Generated/R5/Primitives" -Recurse -Filter *.cs)
    if ($modelFiles.Count -ne $pin.expectedModelSources -or $primitiveFiles.Count -ne $pin.expectedPrimitiveSources) { throw 'Generated source count mismatch.' }
    $files = @($modelFiles) + @($primitiveFiles) + @(Get-ChildItem "$source/Generated/R5" -Recurse -Filter '*manifest.json') +
        @(Get-Item "$source/Tests/Architecture/ApprovedPublicApi.txt", "$source/Tests/Architecture/ApprovedR5ModelApi.txt")
    $hashLines = [Collections.Generic.List[string]]::new()
    foreach ($file in $files) { $hashLines.Add((Hash $file.FullName) + '  ' + [IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')) }
    $hashLines.Sort([StringComparer]::Ordinal)
    WriteText "$output/source-inventory.txt" ($hashLines -join "`n")
    foreach ($name in @('ApprovedPublicApi.txt', 'ApprovedR5ModelApi.txt')) { Copy-Item "$source/Tests/Architecture/$name" "$output/$name" }
    Copy-Item "$source/Generated/R5/model-generation-manifest.json" "$output/model-generation-manifest.json"
    Copy-Item "$source/Generated/R5/Primitives/primitive-generation-manifest.json" "$output/primitive-generation-manifest.json"
    foreach ($manifest in @('model-generation-manifest.json', 'primitive-generation-manifest.json')) {
        if ((Get-Content "$output/$manifest" -Raw | ConvertFrom-Json).schemaVersion -ne $pin.manifestSchemaVersion) { throw "Manifest schema mismatch: $manifest" }
    }
    Complete-KernelGate $result
    $metadata = [ordered]@{
        sourceRevision = $pin.sourceRevision; fixtureRevision = $pin.fixture.revision
        fixtureSourceSha256 = Hash "$fixtureCopy/Program.cs"; fixtureProjectSha256 = Hash "$fixtureCopy/Consumer.csproj"
        sdkVersion = $sdkVersion; configuration = 'Release'; targetFramework = $tfm
        sdkAssemblyIdentity = [Reflection.AssemblyName]::GetAssemblyName($sdk).FullName
        sdkImplementationSha256 = Hash $sdk; compilerOnlyReferenceSha256 = Hash $reference
        descriptorSha256 = Hash $descriptor; packageSha256 = Hash $packages[0].FullName
        primitivePolicySha256 = Hash "$source/CodeGen/Policy/primitive-generation-policy.json"
        fhirArchiveSha256 = Hash "$source/Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz"
        oldConsumerSha256 = $consumerHash; oldConsumerCompileReferenceIdentity = [Reflection.AssemblyName]::GetAssemblyName($sdk).FullName
        runtimeSymbols = $contract.symbols.Count; r5SurfaceTypes = $r5Count
        modelSources = $modelFiles.Count; primitiveSources = $primitiveFiles.Count
        oldConsumerBaselinePassed = $true; currentSourceConsumerPassed = $true
        inventoryDeterministic = $true; splitCompatibilityValidated = $false
        externalClientSmoke = 'Requires MYFHIRSDK_INTEGRATION_BASE_URL; consult TRX for executed/skipped status.'
        regressionAndSmokePassed = $false; priorCiEvidence = $pin.ciEvidence
    }
    foreach ($key in $metadata.Keys) { $result[$key] = $metadata[$key] }
    if ($RunRegressionAndSmoke) {
        Start-KernelGate $result 'regression'
        # The frozen reference belongs to the old consumer/tool. Current regression
        # must build the current SDK reference matching the current descriptor.
        Run dotnet @('test', "$root/MyFhirSdk.sln", '-c', 'Release', '--logger', "trx;LogFilePrefix=k0", '--results-directory', "$output/test-results")
        $counts = [ordered]@{ total = 0; passed = 0; failed = 0; skipped = 0 }
        foreach ($trx in Get-ChildItem "$output/test-results" -Filter *.trx -Recurse) {
            [xml] $report = Get-Content $trx.FullName -Raw
            foreach ($key in @('total', 'passed', 'failed')) { $counts[$key] += [int] $report.TestRun.ResultSummary.Counters.$key }
            # VSTest's summary notExecuted counter may be zero for xUnit skipped tests.
            $counts.skipped += @($report.SelectNodes("//*[local-name()='UnitTestResult' and @outcome='NotExecuted']")).Count
        }
        $result.regressionCounts = $counts
        Complete-KernelGate $result
        Start-KernelGate $result 'primitiveEquivalence'
        & "$root/eng/Export-PrimitiveTgzInputBaseline.ps1" -OutputDirectory "$output/primitive-equivalence"
        Complete-KernelGate $result
        Start-KernelGate $result 'toolSmoke'
        & "$root/eng/Invoke-CodeGenToolSmoke.ps1" -PackagePath $packages[0].FullName -ToolManifestPath "$source/.config/dotnet-tools.json" -FhirPackagePath "$source/Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz" -PrimitiveDefinitionsPath "$source/Tests/CodeGen/Fixtures/StructureDefinitions/Primitives/R5" -PrimitivePolicyPath "$source/CodeGen/Policy/primitive-generation-policy.json" -CommittedGeneratedRoot "$source/Generated/R5" -OutputDirectory "$output/smoke"
        $result.regressionAndSmokePassed = $true
        Complete-KernelGate $result
    }
    Start-KernelGate $result 'inventoryComparison'
    Assert-KernelInventories "$root/docs/gen/baselines/kernel-k0" $output
    Complete-KernelGate $result
}
Write-Output "K0 evidence: $output/evidence.json"
