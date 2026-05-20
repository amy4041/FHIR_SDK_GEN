namespace MyFhirSdk.Client.Authentication;

/// <summary>
/// Applies authentication details to outgoing HTTP requests.
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// Applies authentication to the request.
    /// </summary>
    Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
