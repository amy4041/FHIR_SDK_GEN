using System.Collections.ObjectModel;

namespace MyFhirSdk.Validation;

/// <summary>
/// Result returned by standalone FHIR validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Creates a validation result from issues.
    /// </summary>
    public ValidationResult(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = new ReadOnlyCollection<ValidationIssue>(issues.ToList());
    }

    /// <summary>
    /// Gets an empty successful validation result.
    /// </summary>
    public static ValidationResult Success { get; } = new(Array.Empty<ValidationIssue>());

    /// <summary>
    /// Whether no validation issues were found.
    /// </summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>
    /// Issues found during validation.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }
}
