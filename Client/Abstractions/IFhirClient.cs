using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;
using MyFhirSdk.Resources;

namespace MyFhirSdk.Client.Abstractions;

/// <summary>
/// Public FHIR REST client API.
/// </summary>
public interface IFhirClient
{
    /// <summary>
    /// Reads a resource by logical id, returning null when the server responds with 404 Not Found.
    /// </summary>
    Task<TResource?> ReadAsync<TResource>(
        string id,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    /// <summary>
    /// Creates a resource and returns the server representation.
    /// </summary>
    Task<TResource> CreateAsync<TResource>(
        TResource resource,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    /// <summary>
    /// Updates a resource and returns the server representation.
    /// </summary>
    Task<TResource> UpdateAsync<TResource>(
        TResource resource,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    /// <summary>
    /// Searches the resource type represented by <typeparamref name="TResource"/> with a raw query string.
    /// </summary>
    Task<Bundle> SearchAsync<TResource>(
        string query,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    /// <summary>
    /// Searches the resource type represented by <typeparamref name="TResource"/> with a structured query.
    /// </summary>
    Task<Bundle> SearchAsync<TResource>(
        FhirSearchQuery query,
        CancellationToken cancellationToken = default)
        where TResource : Resource;
}
