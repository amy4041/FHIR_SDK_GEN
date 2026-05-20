using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;

namespace MyFhirSdk.Client.Abstractions;

/// <summary>
/// Builds HTTP requests for FHIR REST interactions.
/// </summary>
public interface IFhirRequestBuilder
{
    /// <summary>
    /// Builds a GET request for a resource instance.
    /// </summary>
    HttpRequestMessage BuildReadRequest<TResource>(string id)
        where TResource : Resource;

    /// <summary>
    /// Builds a POST request for a resource type endpoint.
    /// </summary>
    HttpRequestMessage BuildCreateRequest<TResource>(TResource resource, string json)
        where TResource : Resource;

    /// <summary>
    /// Builds a PUT request for a resource instance endpoint.
    /// </summary>
    HttpRequestMessage BuildUpdateRequest<TResource>(TResource resource, string json)
        where TResource : Resource;

    /// <summary>
    /// Builds a GET request for a raw FHIR search query string.
    /// </summary>
    HttpRequestMessage BuildSearchRequest<TResource>(string query)
        where TResource : Resource;

    /// <summary>
    /// Builds a GET request for a structured FHIR search query.
    /// </summary>
    HttpRequestMessage BuildSearchRequest<TResource>(FhirSearchQuery query)
        where TResource : Resource;
}
