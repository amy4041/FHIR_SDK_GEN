using MyFhirSdk.ImplementationGuides.TwCore.Validation;
using MyFhirSdk.R4.Resources;
using MyFhirSdk.Validation.Profiles;

namespace MyFhirSdk.ImplementationGuides.TwCore;

/// <summary>
/// Manual R4 Patient three-rule POC; does not load the official TW Core package.
/// </summary>
public sealed class TwCorePackage : IImplementationGuidePackage
{
    private static readonly IReadOnlyCollection<string> Profiles =
    [
        TwCoreProfiles.Patient
    ];

    private static readonly IReadOnlyList<IProfileValidationRule> PatientRules =
        TwCorePatientRules.Create();

    /// <summary>
    /// Default TW Core package instance.
    /// </summary>
    public static TwCorePackage Default { get; } = new();

    /// <inheritdoc />
    public string PackageId => "tw.gov.mohw.twcore#1.0.0";

    /// <inheritdoc />
    public string Name => "TW Core";

    /// <inheritdoc />
    public string FhirVersion => "4.0.1";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedProfiles => Profiles;

    /// <inheritdoc />
    public bool SupportsProfile(string profileUrl)
    {
        return Profiles.Contains(profileUrl, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public bool SupportsResource(string profileUrl, Type resourceType) =>
        SupportsProfile(profileUrl) && resourceType == typeof(Patient);

    /// <inheritdoc />
    public IEnumerable<IProfileValidationRule> GetRules(
        string profileUrl,
        Type resourceType)
    {
        if (profileUrl == TwCoreProfiles.Patient && resourceType == typeof(Patient))
        {
            return PatientRules;
        }

        return Array.Empty<IProfileValidationRule>();
    }
}
