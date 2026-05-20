using MyFhirSdk.Core;
using MyFhirSdk.Resources;

namespace MyFhirSdk.Client.Abstractions;

/// <summary>
/// Converts raw HTTP responses into SDK resource objects.
/// </summary>
public interface IFhirResponseHandler
{
    /// <summary>
    /// Handles a resource response where 404 Not Found is represented as null.
    /// </summary>
    Task<TResource?> HandleOptionalResourceAsync<TResource>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    /// <summary>
    /// Handles a resource response that must contain a successful body.
    /// </summary>
    Task<TResource> HandleRequiredResourceAsync<TResource>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    /// <summary>
    /// Handles a search response and parses it as a Bundle.
    /// </summary>
    Task<Bundle> HandleBundleAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default);
}
