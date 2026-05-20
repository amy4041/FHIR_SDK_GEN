using System.Net;
using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Client.Exceptions;
using MyFhirSdk.Core;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization;

namespace MyFhirSdk.Client.Responses;

/// <summary>
/// Parses FHIR HTTP responses into SDK resources.
/// </summary>
public sealed class FhirResponseHandler : IFhirResponseHandler
{
    private readonly IFhirParser _parser;

    /// <summary>
    /// Creates a response handler.
    /// </summary>
    public FhirResponseHandler(IFhirParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <inheritdoc />
    public async Task<TResource?> HandleOptionalResourceAsync<TResource>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await HandleRequiredResourceAsync<TResource>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResource> HandleRequiredResourceAsync<TResource>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(response);

        var json = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw FhirClientException.FromResponse(response, json);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FhirInvalidResponseException("FHIR response body is empty.");
        }

        try
        {
            return _parser.Parse<TResource>(json);
        }
        catch (FhirClientException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FhirInvalidResponseException("FHIR response body could not be parsed.", ex);
        }
    }

    /// <inheritdoc />
    public Task<Bundle> HandleBundleAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        return HandleRequiredResourceAsync<Bundle>(response, cancellationToken);
    }

    private static Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return response.Content is null
            ? Task.FromResult(string.Empty)
            : response.Content.ReadAsStringAsync(cancellationToken);
    }
}
