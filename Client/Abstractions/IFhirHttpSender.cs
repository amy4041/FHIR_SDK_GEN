namespace MyFhirSdk.Client.Abstractions;

/// <summary>
/// Sends prepared FHIR HTTP requests.
/// </summary>
public interface IFhirHttpSender
{
    /// <summary>
    /// Sends the request and returns the raw HTTP response.
    /// </summary>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
