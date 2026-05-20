using System.Net.Http.Headers;

namespace MyFhirSdk.Client.Authentication;

/// <summary>
/// Applies a static bearer token to outgoing FHIR requests.
/// </summary>
public sealed class BearerTokenAuthProvider : IAuthProvider
{
    private readonly string _token;

    /// <summary>
    /// Creates a bearer token provider.
    /// </summary>
    public BearerTokenAuthProvider(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Bearer token cannot be empty.", nameof(token));
        }

        _token = token;
    }

    /// <inheritdoc />
    public Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return Task.CompletedTask;
    }
}
