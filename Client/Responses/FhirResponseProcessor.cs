using System.Net;
using MyFhirSdk.Client.Exceptions;
using MyFhirSdk.Core;
using MyFhirSdk.Serialization;

namespace MyFhirSdk.Client.Responses;

// Parsing and status handling are shared; the composition root supplies the parser and HTTP error factory.
internal sealed class FhirResponseProcessor(IFhirParser parser,
    Func<HttpResponseMessage, string, Exception>? errorFactory = null)
{
    private readonly IFhirParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    internal async Task<T?> OptionalAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : Resource
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await RequiredAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<T> RequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : Resource
    {
        var json = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) throw new FhirInvalidResponseException("FHIR response body is empty.");
        return Parse<T>(json);
    }

    internal async Task<T?> DeleteAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : Resource
    {
        var json = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? null : Parse<T>(json);
    }

    private async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var json = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw errorFactory?.Invoke(response, json) ?? FhirClientException.FromResponse(response, json);
        return json;
    }

    private T Parse<T>(string json) where T : Resource
    {
        try { return _parser.Parse<T>(json); }
        catch (FhirClientException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new FhirInvalidResponseException("FHIR response body could not be parsed.", ex); }
    }
}
