using MyFhirSdk.Core;

namespace MyFhirSdk.Validation.Profiles;

/// <summary>
/// Context passed to one profile validation rule.
/// </summary>
public sealed class ProfileValidationContext
{
    /// <summary>
    /// Resource being validated.
    /// </summary>
    public required Resource Resource { get; init; }

    /// <summary>
    /// Package identifier for the rule source.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Profile canonical URL being validated.
    /// </summary>
    public required string ProfileUrl { get; init; }

    /// <summary>
    /// Stable validation rule identifier.
    /// </summary>
    public required string RuleId { get; init; }
}
