namespace MyFhirSdk.Client.Exceptions;

/// <summary>
/// Represents a malformed, empty, or unparsable FHIR response.
/// </summary>
public sealed class FhirInvalidResponseException : FhirClientException
{
    /// <summary>
    /// Creates an invalid response exception.
    /// </summary>
    public FhirInvalidResponseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates an invalid response exception with an inner exception.
    /// </summary>
    public FhirInvalidResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
