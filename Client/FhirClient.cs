using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Http;
using MyFhirSdk.Client.Requests;
using MyFhirSdk.Client.Responses;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Client;

/// <summary>The compatibility R5 REST client. Use the typed client for explicit version composition.</summary>
public sealed class FhirClient : IFhirClient
{
    private readonly FhirClientExecutor _executor;
    private readonly IFhirRequestBuilder _requests;
    private readonly IFhirResponseHandler _responses;

    public FhirClient(HttpClient httpClient, IFhirSerializer serializer, IFhirParser parser,
        FhirClientOptions options, IAuthProvider? authProvider = null, IFhirValidator? validator = null)
        : this(serializer,
            new FhirRequestBuilder(new FhirResourceTypeResolver(), new FhirRequestUriBuilder(options.BaseAddress)),
            new FhirHttpSender(FhirClientExecutor.Configure(httpClient, options)), new FhirResponseHandler(parser),
            authProvider, options.ValidateBeforeSend, validator) { }

    public FhirClient(IFhirSerializer serializer, IFhirRequestBuilder requestBuilder, IFhirHttpSender httpSender,
        IFhirResponseHandler responseHandler, IAuthProvider? authProvider = null, bool validateBeforeSend = false,
        IFhirValidator? validator = null)
    {
        _requests = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        _responses = responseHandler ?? throw new ArgumentNullException(nameof(responseHandler));
        _executor = new(serializer, httpSender, authProvider, validator, validateBeforeSend);
    }

    public async Task<TResource?> ReadAsync<TResource>(string id, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildReadRequest<TResource>(id), _responses.HandleOptionalResourceAsync<TResource>, cancellationToken).ConfigureAwait(false);

    public async Task<TResource> CreateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildCreateRequest(resource, _executor.Serialize(resource)), _responses.HandleRequiredResourceAsync<TResource>, cancellationToken).ConfigureAwait(false);

    public async Task<TResource> UpdateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildUpdateRequest(resource, _executor.Serialize(resource)), _responses.HandleRequiredResourceAsync<TResource>, cancellationToken).ConfigureAwait(false);

    public async Task<Bundle> SearchAsync<TResource>(string query, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildSearchRequest<TResource>(query), _responses.HandleBundleAsync, cancellationToken).ConfigureAwait(false);

    public async Task<Bundle> SearchAsync<TResource>(FhirSearchQuery query, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildSearchRequest<TResource>(query), _responses.HandleBundleAsync, cancellationToken).ConfigureAwait(false);
}
