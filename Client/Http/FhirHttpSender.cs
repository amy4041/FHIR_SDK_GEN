using MyFhirSdk.Client.Abstractions;

namespace MyFhirSdk.Client.Http;

/// <summary>
/// Default HTTP sender backed by <see cref="HttpClient"/>.
/// </summary>
public sealed class FhirHttpSender : IFhirHttpSender
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a sender.
    /// </summary>
    public FhirHttpSender(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _httpClient.SendAsync(request, cancellationToken);
    }
}
