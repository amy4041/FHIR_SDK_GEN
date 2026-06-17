namespace MyFhirSdk.Validation;

/// <summary>
/// A single validation issue found in a FHIR object graph.
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// FHIR-style path to the invalid field.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable validation message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Issue severity.
    /// </summary>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;

    /// <summary>
    /// Machine-readable issue category.
    /// </summary>
    public ValidationIssueCode Code { get; init; }

    /// <summary>
    /// Validation layer that produced this issue.
    /// </summary>
    public ValidationRuleSource Source { get; init; } = ValidationRuleSource.BaseFhir;

    /// <summary>
    /// IG package identifier that produced this issue, when applicable.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Profile canonical URL that produced this issue, when applicable.
    /// </summary>
    public string? ProfileUrl { get; init; }

    /// <summary>
    /// Stable validation rule identifier that produced this issue, when available.
    /// </summary>
    public string? RuleId { get; init; }
}
