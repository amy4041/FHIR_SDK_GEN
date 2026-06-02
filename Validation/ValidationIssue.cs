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
}
