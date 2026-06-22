namespace MyFhirSdk.Validation.Profiles;

/// <summary>
/// Provides metadata and validation rules for an implementation guide package.
/// </summary>
public interface IImplementationGuidePackage
{
    /// <summary>
    /// Package identifier, including version when known.
    /// </summary>
    string PackageId { get; }

    /// <summary>
    /// Human-readable package name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// FHIR release targeted by this package.
    /// </summary>
    string FhirVersion { get; }

    /// <summary>
    /// Profile canonical URLs supported by this package.
    /// </summary>
    IReadOnlyCollection<string> SupportedProfiles { get; }

    /// <summary>
    /// Returns whether this package supports a profile canonical URL.
    /// </summary>
    bool SupportsProfile(string profileUrl);

    /// <summary>
    /// Gets validation rules for a profile and resource type.
    /// </summary>
    IEnumerable<IProfileValidationRule> GetRules(
        string profileUrl,
        Type resourceType);
}
