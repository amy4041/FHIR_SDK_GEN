namespace MyFhirSdk.Validation.Profiles;

/// <summary>
/// Validates one profile-specific rule against a resource.
/// </summary>
public interface IProfileValidationRule
{
    /// <summary>
    /// Stable validation rule identifier.
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Validates a profile rule and appends any issues.
    /// </summary>
    void Validate(
        ProfileValidationContext context,
        ICollection<ValidationIssue> issues);
}
