using System.Net;

namespace MyFhirSdk.Client.Exceptions;

/// <summary>
/// Represents a non-success HTTP response from a FHIR server.
/// </summary>
public sealed class FhirHttpException : FhirClientException
{
    /// <summary>
    /// Creates an HTTP exception.
    /// </summary>
    public FhirHttpException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string? responseBody,
        HttpMethod? method,
        Uri? requestUri)
        : base(CreateMessage(statusCode, reasonPhrase, method, requestUri))
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        ResponseBody = responseBody;
        Method = method;
        RequestUri = requestUri;
    }

    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// HTTP reason phrase returned by the server, when available.
    /// </summary>
    public string? ReasonPhrase { get; }

    /// <summary>
    /// Raw response body returned by the server, when available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Request method that produced the response, when available.
    /// </summary>
    public HttpMethod? Method { get; }

    /// <summary>
    /// Request URI that produced the response, when available.
    /// </summary>
    public Uri? RequestUri { get; }

    private static string CreateMessage(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        HttpMethod? method,
        Uri? requestUri)
    {
        var requestDescription = method is null || requestUri is null
            ? "FHIR request"
            : $"{method} {requestUri}";

        return $"{requestDescription} failed with HTTP {(int)statusCode} {reasonPhrase ?? statusCode.ToString()}.";
    }
}
