Set-StrictMode -Version Latest

function ConvertTo-MyFhirSdkSemanticVersion {
    param([Parameter(Mandatory)] [string] $Version)

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $Version,
        '^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)(?:-(?<prerelease>[0-9A-Za-z.-]+))?(?:\+(?<build>[0-9A-Za-z.-]+))?$')
    if (-not $match.Success) {
        throw "Invalid semantic version '$Version'."
    }
    [string[]] $prerelease = @()
    if ($match.Groups['prerelease'].Success) {
        $prerelease = $match.Groups['prerelease'].Value.Split('.')
    }
    return [pscustomobject]@{
        Original = $Version
        Major = [System.Numerics.BigInteger]::Parse($match.Groups['major'].Value)
        Minor = [System.Numerics.BigInteger]::Parse($match.Groups['minor'].Value)
        Patch = [System.Numerics.BigInteger]::Parse($match.Groups['patch'].Value)
        Prerelease = $prerelease
        Build = if ($match.Groups['build'].Success) {
            $match.Groups['build'].Value
        }
        else {
            $null
        }
    }
}

function Compare-MyFhirSdkSemanticVersion {
    param(
        [Parameter(Mandatory)] [string] $Left,
        [Parameter(Mandatory)] [string] $Right
    )

    $leftVersion = ConvertTo-MyFhirSdkSemanticVersion $Left
    $rightVersion = ConvertTo-MyFhirSdkSemanticVersion $Right
    foreach ($component in @('Major', 'Minor', 'Patch')) {
        if ($leftVersion.$component -lt $rightVersion.$component) {
            return -1
        }
        if ($leftVersion.$component -gt $rightVersion.$component) {
            return 1
        }
    }

    if ($leftVersion.Prerelease.Count -eq 0 -and $rightVersion.Prerelease.Count -eq 0) {
        return 0
    }
    if ($leftVersion.Prerelease.Count -eq 0) {
        return 1
    }
    if ($rightVersion.Prerelease.Count -eq 0) {
        return -1
    }

    $identifierCount = [System.Math]::Max(
        $leftVersion.Prerelease.Count,
        $rightVersion.Prerelease.Count)
    for ($index = 0; $index -lt $identifierCount; $index++) {
        if ($index -ge $leftVersion.Prerelease.Count) {
            return -1
        }
        if ($index -ge $rightVersion.Prerelease.Count) {
            return 1
        }
        $leftIdentifier = $leftVersion.Prerelease[$index]
        $rightIdentifier = $rightVersion.Prerelease[$index]
        $leftNumeric = $leftIdentifier -match '^[0-9]+$'
        $rightNumeric = $rightIdentifier -match '^[0-9]+$'
        if ($leftNumeric -and $rightNumeric) {
            $leftNumber = [System.Numerics.BigInteger]::Parse($leftIdentifier)
            $rightNumber = [System.Numerics.BigInteger]::Parse($rightIdentifier)
            if ($leftNumber -lt $rightNumber) {
                return -1
            }
            if ($leftNumber -gt $rightNumber) {
                return 1
            }
            continue
        }
        if ($leftNumeric) {
            return -1
        }
        if ($rightNumeric) {
            return 1
        }
        $comparison = [string]::CompareOrdinal($leftIdentifier, $rightIdentifier)
        if ($comparison -lt 0) {
            return -1
        }
        if ($comparison -gt 0) {
            return 1
        }
    }
    return 0
}
