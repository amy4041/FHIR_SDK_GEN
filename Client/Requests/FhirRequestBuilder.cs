using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Client.Http;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;

namespace MyFhirSdk.Client.Requests;

/// <summary>
/// Builds FHIR REST HTTP request messages.
/// </summary>
public sealed class FhirRequestBuilder : IFhirRequestBuilder
{
    private readonly FhirResourceTypeResolver _resourceTypeResolver;
    private readonly FhirRequestUriBuilder _uriBuilder;

    /// <summary>
    /// Creates a request builder.
    /// </summary>
    public FhirRequestBuilder(
        FhirResourceTypeResolver resourceTypeResolver,
        FhirRequestUriBuilder uriBuilder)
    {
        _resourceTypeResolver = resourceTypeResolver ?? throw new ArgumentNullException(nameof(resourceTypeResolver));
        _uriBuilder = uriBuilder ?? throw new ArgumentNullException(nameof(uriBuilder));
    }

    /// <inheritdoc />
    public HttpRequestMessage BuildReadRequest<TResource>(string id)
        where TResource : Resource
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("FHIR resource id cannot be empty.", nameof(id));
        }

        var resourceType = _resourceTypeResolver.GetResourceType<TResource>();
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            _uriBuilder.BuildResourceInstanceUri(resourceType, id));

        FhirHttpHeaders.AddFhirJsonAccept(request);
        return request;
    }

    /// <inheritdoc />
    public HttpRequestMessage BuildCreateRequest<TResource>(TResource resource, string json)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(json);

        var resourceType = _resourceTypeResolver.GetResourceType(resource);
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            _uriBuilder.BuildResourceTypeUri(resourceType));

        FhirHttpHeaders.AddFhirJsonAccept(request);
        FhirHttpHeaders.AddReturnRepresentationPrefer(request);
        request.Content = FhirHttpContent.CreateJson(json);

        return request;
    }

    /// <inheritdoc />
    public HttpRequestMessage BuildUpdateRequest<TResource>(TResource resource, string json)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(resource.Id))
        {
            throw new ArgumentException("FHIR resource id is required for update.", nameof(resource));
        }

        var resourceType = _resourceTypeResolver.GetResourceType(resource);
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            _uriBuilder.BuildResourceInstanceUri(resourceType, resource.Id));

        FhirHttpHeaders.AddFhirJsonAccept(request);
        FhirHttpHeaders.AddReturnRepresentationPrefer(request);
        request.Content = FhirHttpContent.CreateJson(json);

        return request;
    }

    /// <inheritdoc />
    public HttpRequestMessage BuildSearchRequest<TResource>(string query)
        where TResource : Resource
    {
        var resourceType = _resourceTypeResolver.GetResourceType<TResource>();
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            _uriBuilder.BuildSearchUri(resourceType, query));

        FhirHttpHeaders.AddFhirJsonAccept(request);
        return request;
    }

    /// <inheritdoc />
    public HttpRequestMessage BuildSearchRequest<TResource>(FhirSearchQuery query)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(query);

        return BuildSearchRequest<TResource>(query.ToQueryString());
    }
}
