using MyFhirSdk.Validation;

namespace MyFhirSdk.Client.Exceptions;

/// <summary>
/// Represents a client-side validation failure before a resource request is sent.
/// </summary>
public sealed class FhirValidationException : FhirClientException
{
    /// <summary>
    /// Creates a validation exception from the failed validation result.
    /// </summary>
    public FhirValidationException(ValidationResult result)
        : base(CreateMessage(result))
    {
        Result = result;
    }

    /// <summary>
    /// Full validation result that prevented the client request from being sent.
    /// </summary>
    public ValidationResult Result { get; }

    private static string CreateMessage(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return $"FHIR resource validation failed with {result.Issues.Count} issue(s).";
    }
}
