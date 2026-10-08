#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/kernel-k3',
    [string] $RuntimeReferenceAssetPath = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'KernelMigration.Common.ps1')
. (Join-Path $PSScriptRoot 'KernelConsumerRebuild.Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith((Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar, $comparison) -or (Test-Path -LiteralPath $output)) {
    throw 'Use a fresh directory under artifacts/.'
}
New-Item -ItemType Directory -Path $output | Out-Null
[xml] $props = Get-Content "$root/Directory.Build.props" -Raw
$tfm = [string] $props.Project.PropertyGroup.MyFhirSdkTargetFramework
function Hash([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function DeployedRun([string] $Directory, [string] $Assembly, [string] $LogName) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add((Join-Path $Directory $Assembly))
    $start.Environment['DOTNET_CLI_UI_LANGUAGE'] = Get-KernelBuildUiLanguage
    foreach ($name in @('DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE', 'DOTNET_STARTUP_HOOKS')) { $start.Environment.Remove($name) | Out-Null }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) { $process.Kill($true); throw 'Isolated consumer timed out.' }
        $result = [ordered]@{ exitCode = $process.ExitCode; stdout = $stdout.GetAwaiter().GetResult(); stderr = $stderr.GetAwaiter().GetResult() }
        Write-KernelJson "$output/$LogName.json" $result
        return $result
    } finally { $process.Dispose() }
}
$evidence = [ordered]@{
    schemaVersion = 1; status = 'running'; activeGate = $null; failure = $null
    migrationPolicy = 'rebuild-all-no-forwarders'; targetFramework = $tfm
    gates = [ordered]@{ consumerInventory = 'pending'; cleanRegression = 'pending'; rebuiltConsumers = 'pending'; deployment = 'pending'; missingRuntime = 'pending'; splitInventory = 'pending'; historicalBaseline = 'pending' }
}
Invoke-KernelEvidenceRun -Evidence $evidence -OutputPath "$output/evidence.json" -Action {
    param($evidence)
    Start-KernelGate $evidence 'consumerInventory'
    $projects = @(Get-KernelConsumerProjects $root)
    $sdkTests = @(
        'Tests/Architecture/MyFhirSdk.Architecture.Tests.csproj', 'Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj',
        'Tests/Client/MyFhirSdk.Client.Tests.csproj', 'Tests/Parser/Json/MyFhirSdk.Parser.Json.Tests.csproj',
        'Tests/Serializer/Json/MyFhirSdk.Serialization.Json.Tests.csproj', 'Tests/Validation/MyFhirSdk.Validation.Tests.csproj',
        'Tests/ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.Tests.csproj'
    )
    $special = @{
        'MyFhirSdk.csproj' = 'production-sdk'; 'Runtime/MyFhirSdk.Runtime.csproj' = 'production-runtime'
        'CodeGen/MyFhirSdk.CodeGen.csproj' = 'production-metadata-only-generator'
        'Tests/KernelMigration/BaselineConsumer/v1/Consumer.csproj' = 'frozen-historical-only-K0'
        'Tests/KernelMigration/Consumer/Consumer.csproj' = 'current-explicit-DLL-consumer'
        'Tests/KernelMigration/RebuiltLibrary/RebuiltLibrary.csproj' = 'rebuilt-dependent-library'
        'Tests/KernelMigration/RebuiltConsumer/RebuiltConsumer.csproj' = 'rebuilt-application'
        'Tests/KernelMigration/Inventory/Inventory.csproj' = 'explicit-implementation-inventory-tool'
    }
    Assert-KernelConsumerProjectSet -Projects $projects -ExpectedProjects ($sdkTests + @($special.Keys))
    $inventory = foreach ($project in $projects | Sort-Object) {
        [xml] $xml = Get-Content (Join-Path $root $project) -Raw
        $references = @($xml.SelectNodes('//ProjectReference') | ForEach-Object { $_.GetAttribute('Include') })
        $dllReferences = @($xml.SelectNodes('//Reference') | ForEach-Object { $_.GetAttribute('Include') })
        $role = if ($project -in $sdkTests) { 'sdk-test-consumer' } elseif ($special.ContainsKey($project)) { $special[$project] } else { throw "Unclassified consumer project: $project" }
        if ($role -eq 'sdk-test-consumer' -and -not @($references | Where-Object { $_ -match '(^|[\\/])MyFhirSdk.csproj$' }).Count) { throw "SDK consumer reference missing: $project" }
        $command = if ($role -eq 'sdk-test-consumer') { "dotnet test $project -c Release --logger trx" }
            elseif ($role -like 'production-*') { 'dotnet test MyFhirSdk.sln -c Release --logger trx (builds production dependencies)' }
            elseif ($role -eq 'frozen-historical-only-K0') { 'pwsh -File eng/Test-KernelMigrationBaseline.ps1' }
            elseif ($role -eq 'explicit-implementation-inventory-tool') { 'dotnet build <fresh-staging>/Inventory/Inventory.csproj; dotnet Inventory.dll <SDK> <output> <Runtime>' }
            else { 'dotnet build <fresh-staging>/' + [IO.Path]::GetFileName($project) + ' -c Release -p:SdkAssemblyPath=<SDK implementation> -p:RuntimeAssemblyPath=<Runtime implementation>' }
        [ordered]@{ source = $project; role = $role; projectReferences = $references; assemblyReferences = $dllReferences; rebuildCommand = $command }
    }
    if (@($inventory | Where-Object { $_.role -eq 'sdk-test-consumer' }).Count -ne $sdkTests.Count -or $projects.Count -ne ($sdkTests.Count + $special.Count)) { throw 'Consumer inventory is incomplete.' }
    Write-KernelJson "$output/consumer-inventory.json" ([ordered]@{
        projects = @($inventory)
        dynamicConsumers = @('Tests/Architecture/RuntimeContractCompilationTests.cs', 'Tests/CodeGen/Runtime/GeneratedModelTestCompiler.cs', 'Tests/CodeGen/Runtime/RealSdkSourceCompiler.cs', 'Tests/CodeGen/Runtime/ModelMetadataGeneratedRuntimeTests.cs')
        solutionCommand = 'dotnet test MyFhirSdk.sln -c Release --logger trx'
        explicitReferenceCommand = 'dotnet build <staged-project> -c Release -p:SdkAssemblyPath=<implementation> -p:RuntimeAssemblyPath=<implementation>'
        deploymentDependencies = @('MyFhirSdk.dll', 'MyFhirSdk.Runtime.dll', 'RebuiltLibrary.dll', 'RebuiltConsumer.dll', 'RebuiltConsumer.deps.json', 'RebuiltConsumer.runtimeconfig.json')
        historicalCommand = 'pwsh -File eng/Test-KernelMigrationBaseline.ps1 -RunRegressionAndSmoke'
        generatorBoundary = 'No SDK/Runtime ProjectReference; legacy compiler asset migration remains K4.'
    })
    $protected = @(Get-Item "$root/eng/kernel-migration-baseline.json") + @(Get-ChildItem "$root/docs/gen/baselines/kernel-k0", "$root/Tests/KernelMigration/BaselineConsumer/v1" -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    $before = @{}; foreach ($file in $protected) { $before[$file.FullName] = Hash $file.FullName }
    $pin = Get-Content "$root/eng/kernel-migration-baseline.json" -Raw | ConvertFrom-Json
    Copy-KernelBaselineFixture $root $pin.fixture "$output/frozen-fixture-check"
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'cleanRegression'
    Invoke-KernelDotNet @('restore', "$root/MyFhirSdk.sln")
    Invoke-KernelDotNet @('clean', "$root/MyFhirSdk.sln", '-c', 'Release', '-v', 'quiet')
    $testArguments = @('test', "$root/MyFhirSdk.sln", '-c', 'Release', '--no-restore', '--logger', 'trx;LogFilePrefix=k3', '--results-directory', "$output/test-results")
    if ($RuntimeReferenceAssetPath) {
        $referencePath = [IO.Path]::GetFullPath($RuntimeReferenceAssetPath, $root)
        if (-not (Test-Path -LiteralPath $referencePath)) { throw 'Explicit compiler reference asset is missing.' }
        $testArguments += "-p:RuntimeReferenceAssetPath=$referencePath"
    }
    Invoke-KernelDotNet $testArguments
    $regression = Assert-KernelConsumerRegression -ReportsDirectory "$output/test-results" -ExpectedAssemblies @(
        $sdkTests | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) + '.dll' }
    )
    $evidence.regressionCounts = $regression.counts
    $evidence.regressionProjects = $regression.projects
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'rebuiltConsumers'
    $sdk = "$root/bin/Release/$tfm/MyFhirSdk.dll"
    $runtime = "$root/bin/Release/$tfm/MyFhirSdk.Runtime.dll"
    foreach ($name in @('Consumer', 'RebuiltLibrary', 'RebuiltConsumer', 'Inventory')) {
        $destination = "$output/source/$name"
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Get-ChildItem "$root/Tests/KernelMigration/$name" -File | Copy-Item -Destination $destination
    }
    $properties = @("-p:MyFhirSdkTargetFramework=$tfm", "-p:SdkAssemblyPath=$sdk", "-p:RuntimeAssemblyPath=$runtime")
    Invoke-KernelDotNet (@('build', "$output/source/Consumer/Consumer.csproj", '-c', 'Release', '-o', "$output/current-consumer") + $properties)
    Invoke-KernelDotNet (@('build', "$output/source/RebuiltConsumer/RebuiltConsumer.csproj", '-c', 'Release', '-o', "$output/chain-build") + $properties)
    Invoke-KernelDotNet @('build', "$output/source/Inventory/Inventory.csproj", '-c', 'Release', "-p:MyFhirSdkTargetFramework=$tfm", '-o', "$output/inventory-tool")
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'deployment'
    $current = DeployedRun "$output/current-consumer" 'Consumer.dll' 'current-consumer-run'
    if ($current.exitCode -ne 0 -or -not $current.stdout.Contains('Consumer passed:')) { throw 'Current consumer execution failed.' }
    $deployment = "$output/deployment"
    New-Item -ItemType Directory -Path $deployment | Out-Null
    Get-ChildItem "$output/chain-build" -File | Copy-Item -Destination $deployment
    foreach ($name in @('MyFhirSdk.dll', 'MyFhirSdk.Runtime.dll', 'RebuiltLibrary.dll', 'RebuiltConsumer.dll', 'RebuiltConsumer.deps.json', 'RebuiltConsumer.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath "$deployment/$name")) { throw "Missing deployment file: $name" }
    }
    $run = DeployedRun $deployment 'RebuiltConsumer.dll' 'deployment-run'
    if ($run.exitCode -ne 0) { throw "Rebuilt chain failed: $($run.stderr)" }
    $behavior = $run.stdout.Trim() | ConvertFrom-Json
    if ($behavior.status -cne 'passed' -or @($behavior.assemblies).Count -ne 4) { throw 'Incomplete deployment evidence.' }
    $evidence.deployment = $behavior
    $evidence.deployedHashes = @($behavior.assemblies | ForEach-Object { [ordered]@{ identity = $_.identity; sha256 = Hash $_.path } })
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'missingRuntime'
    # Only this verified deployment copy is changed; build outputs remain intact.
    $missing = [IO.Path]::GetFullPath("$deployment/MyFhirSdk.Runtime.dll")
    if (-not $missing.StartsWith($output + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'Invalid negative-test path.' }
    Remove-Item -LiteralPath $missing
    try {
        $negative = DeployedRun $deployment 'RebuiltConsumer.dll' 'missing-runtime-run'
        if ($negative.exitCode -eq 0 -or -not $negative.stderr.Contains('MyFhirSdk.Runtime') -or -not $negative.stderr.Contains('FileNotFoundException')) { throw 'Missing Runtime did not produce the required dependency failure.' }
        $evidence.missingRuntime = [ordered]@{ exitCode = $negative.exitCode; dependency = 'MyFhirSdk.Runtime'; fallbackObserved = $false }
    } finally { Copy-Item -LiteralPath "$output/chain-build/MyFhirSdk.Runtime.dll" -Destination $missing }
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'splitInventory'
    Invoke-KernelDotNet @("$output/inventory-tool/Inventory.dll", $sdk, "$output/public-api.txt", $runtime)
    Invoke-KernelDotNet @("$output/inventory-tool/Inventory.dll", $sdk, "$output/public-api-repeat.txt", $runtime)
    if ((Hash "$output/public-api.txt") -cne (Hash "$output/public-api-repeat.txt")) { throw 'Split API inventory is not deterministic.' }
    $api = Get-Content "$output/public-api.txt"
    $runtimeTypes = @($api | Where-Object { $_ -match '^TYPE \[MyFhirSdk.Runtime\]' }).Count
    if ($runtimeTypes -ne 14) { throw "Expected 14 Runtime declarations, got $runtimeTypes." }
    $evidence.runtimeDeclarations = $runtimeTypes
    $evidence.publicApiSha256 = Hash "$output/public-api.txt"
    Complete-KernelGate $evidence

    Start-KernelGate $evidence 'historicalBaseline'
    foreach ($file in $protected) { if ((Hash $file.FullName) -cne $before[$file.FullName]) { throw "Historical baseline changed: $($file.FullName)" } }
    $evidence.historicalBaseline = 'Frozen fixture pin verified; baseline files unchanged. Historical execution remains the separate K0 gate.'
    Complete-KernelGate $evidence
}
Write-Output "K3 evidence: $output/evidence.json"
# The expected negative dependency failure must not fail the GitHub Actions wrapper.
exit 0
