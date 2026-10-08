# K3 discovery and regression validation are shared with the failure-mode harness.
function Get-KernelConsumerProjects {
    param([Parameter(Mandatory)] [string] $RepositoryRoot)
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($RepositoryRoot))
    $projects = [Collections.Generic.List[string]]::new()
    $excluded = @('artifacts', 'bin', 'obj', '.git', '.codex', '.agents', '.aws', '.vs', '.idea')
    while ($pending.Count) {
        foreach ($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($entry.PSIsContainer) {
                if ($entry.Name -notin $excluded -and -not ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { $pending.Push($entry.FullName) }
            } elseif ($entry.Extension -eq '.csproj') {
                $projects.Add([IO.Path]::GetRelativePath($RepositoryRoot, $entry.FullName).Replace('\', '/'))
            }
        }
    }
    $projects.Sort([StringComparer]::Ordinal)
    $projects.ToArray()
}

function Assert-KernelConsumerProjectSet {
    param([string[]] $Projects, [string[]] $ExpectedProjects)
    foreach ($project in $Projects) {
        if ($project -cnotin $ExpectedProjects) { throw "Unclassified consumer project: $project" }
    }
    foreach ($project in $ExpectedProjects) {
        if ($project -cnotin $Projects) { throw "Missing consumer project: $project" }
    }
}

function Assert-KernelConsumerRegression {
    param([Parameter(Mandatory)] [string] $ReportsDirectory, [Parameter(Mandatory)] [string[]] $ExpectedAssemblies)
    $reports = @(Get-ChildItem -LiteralPath $ReportsDirectory -Recurse -Filter *.trx -File)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $counts = [ordered]@{ total = 0; passed = 0; failed = 0; skipped = 0 }
    $perProject = [Collections.Generic.List[object]]::new()
    $smokeName = 'MyFhirSdk.Tests.Client.FhirClientIntegrationSmokeTests.PatientCrudSearchSmokeFlow'
    foreach ($trx in $reports) {
        [xml] $report = Get-Content -LiteralPath $trx.FullName -Raw
        $definitions = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
        $assemblyNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($definition in $report.SelectNodes("//*[local-name()='TestDefinitions']/*[local-name()='UnitTest']")) {
            $method = $definition.SelectSingleNode("*[local-name()='TestMethod']")
            if ($null -eq $method -or -not $method.GetAttribute('codeBase')) { throw "Missing test assembly identity: $($trx.Name)" }
            $assembly = ($method.GetAttribute('codeBase').Replace('\', '/').Split('/'))[-1]
            $assemblyNames.Add($assembly) | Out-Null
            $definitions.Add($definition.GetAttribute('id'), $definition)
        }
        if ($assemblyNames.Count -ne 1) { throw "Expected one test assembly per report: $($trx.Name)" }
        $assembly = @($assemblyNames)[0]
        if ($assembly -cnotin $ExpectedAssemblies) { throw "Unexpected test assembly: $assembly" }
        if (-not $seen.Add($assembly)) { throw "Duplicate test assembly report: $assembly" }
        $results = @($report.SelectNodes("//*[local-name()='Results']/*[local-name()='UnitTestResult']"))
        $projectCounts = [ordered]@{ assembly = $assembly; total = $results.Count; passed = 0; failed = 0; skipped = 0 }
        foreach ($result in $results) {
            if (-not $definitions.ContainsKey($result.GetAttribute('testId'))) { throw "Test result has no definition: $assembly" }
            switch -CaseSensitive ($result.GetAttribute('outcome')) {
                'Passed' { $projectCounts.passed++ }
                'NotExecuted' {
                    $reason = $result.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")
                    if ($assembly -cne 'MyFhirSdk.Client.Tests.dll' -or $result.GetAttribute('testName') -cne $smokeName -or
                        -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('MYFHIRSDK_INTEGRATION_BASE_URL')) -or
                        $null -eq $reason -or $reason.InnerText -cne 'Set MYFHIRSDK_INTEGRATION_BASE_URL to run integration smoke tests.') {
                        throw "Unexpected skipped test: $assembly / $($result.GetAttribute('testName'))"
                    }
                    $projectCounts.skipped++
                }
                default { throw "Unsuccessful test result: $assembly / $($result.GetAttribute('outcome'))" }
            }
        }
        if ($projectCounts.passed -eq 0) { throw "No tests executed successfully: $assembly" }
        $counter = $report.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
        if ($null -eq $counter) { throw "Missing regression counters: $assembly" }
        foreach ($key in @('total', 'passed', 'failed')) {
            if (-not $counter.HasAttribute($key) -or [int] $counter.GetAttribute($key) -ne $projectCounts[$key]) { throw "Regression counter mismatch: $assembly / $key" }
        }
        # xUnit TRX notExecuted may be zero for skipped cases; use actual result rows.
        foreach ($key in @($counts.Keys)) { $counts[$key] += $projectCounts[$key] }
        $perProject.Add($projectCounts)
    }
    foreach ($assembly in $ExpectedAssemblies) {
        if (-not $seen.Contains($assembly)) { throw "Missing test assembly report: $assembly" }
    }
    [ordered]@{ counts = $counts; projects = $perProject.ToArray() }
}
