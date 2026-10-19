using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Core;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization;

namespace MyFhirSdk.Client.Responses;

/// <summary>Compatibility R5 response handler.</summary>
public sealed class FhirResponseHandler : IFhirResponseHandler
{
    private readonly FhirResponseProcessor _responses;
    public FhirResponseHandler(IFhirParser parser) => _responses = new(parser);
    public Task<TResource?> HandleOptionalResourceAsync<TResource>(HttpResponseMessage response, CancellationToken cancellationToken = default) where TResource : Resource
        => _responses.OptionalAsync<TResource>(response, cancellationToken);
    public Task<TResource> HandleRequiredResourceAsync<TResource>(HttpResponseMessage response, CancellationToken cancellationToken = default) where TResource : Resource
        => _responses.RequiredAsync<TResource>(response, cancellationToken);
    public Task<Bundle> HandleBundleAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
        => _responses.RequiredAsync<Bundle>(response, cancellationToken);
}
