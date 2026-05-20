using MyFhirSdk.Core;

namespace MyFhirSdk.Client.Exceptions;

/// <summary>
/// Base exception type for client-layer failures.
/// </summary>
public class FhirClientException : FhirSdkException
{
    /// <summary>
    /// Creates an empty client exception.
    /// </summary>
    public FhirClientException()
    {
    }

    /// <summary>
    /// Creates a client exception with a message.
    /// </summary>
    public FhirClientException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a client exception with a message and an inner exception.
    /// </summary>
    public FhirClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an HTTP exception from a non-success response.
    /// </summary>
    public static FhirHttpException FromResponse(HttpResponseMessage response, string? responseBody)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new FhirHttpException(
            response.StatusCode,
            response.ReasonPhrase,
            responseBody,
            response.RequestMessage?.Method,
            response.RequestMessage?.RequestUri);
    }
}
