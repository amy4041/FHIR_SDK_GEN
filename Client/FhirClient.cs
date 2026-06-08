using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Exceptions;
using MyFhirSdk.Client.Requests;
using MyFhirSdk.Client.Responses;
using MyFhirSdk.Client.Http;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Client;

/// <summary>
/// Default FHIR REST client implementation for read, create, update, and search operations.
/// </summary>
public sealed class FhirClient : IFhirClient
{
    private readonly IFhirSerializer _serializer;
    private readonly IFhirRequestBuilder _requestBuilder;
    private readonly IFhirHttpSender _httpSender;
    private readonly IFhirResponseHandler _responseHandler;
    private readonly IAuthProvider _authProvider;
    private readonly IFhirValidator _validator;
    private readonly bool _validateBeforeSend;

    /// <summary>
    /// Creates a client from the SDK serializer/parser and an <see cref="HttpClient"/>.
    /// </summary>
    public FhirClient(
        HttpClient httpClient,
        IFhirSerializer serializer,
        IFhirParser parser,
        FhirClientOptions options,
        IAuthProvider? authProvider = null,
        IFhirValidator? validator = null)
        : this(
            serializer,
            new FhirRequestBuilder(
                new FhirResourceTypeResolver(),
                new FhirRequestUriBuilder(options.BaseAddress)),
            new FhirHttpSender(ConfigureHttpClient(httpClient, options)),
            new FhirResponseHandler(parser),
            authProvider,
            options.ValidateBeforeSend,
            validator)
    {
    }

    /// <summary>
    /// Creates a client from replaceable collaborators, which is useful for tests and DI.
    /// </summary>
    public FhirClient(
        IFhirSerializer serializer,
        IFhirRequestBuilder requestBuilder,
        IFhirHttpSender httpSender,
        IFhirResponseHandler responseHandler,
        IAuthProvider? authProvider = null,
        bool validateBeforeSend = false,
        IFhirValidator? validator = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        _httpSender = httpSender ?? throw new ArgumentNullException(nameof(httpSender));
        _responseHandler = responseHandler ?? throw new ArgumentNullException(nameof(responseHandler));
        _authProvider = authProvider ?? NoAuthProvider.Instance;
        _validateBeforeSend = validateBeforeSend;
        _validator = validator ?? new FhirValidator();
    }

    /// <inheritdoc />
    public async Task<TResource?> ReadAsync<TResource>(
        string id,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        var request = _requestBuilder.BuildReadRequest<TResource>(id);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return await _responseHandler
            .HandleOptionalResourceAsync<TResource>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResource> CreateAsync<TResource>(
        TResource resource,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(resource);

        ValidateResourceBeforeSend(resource);

        var json = _serializer.Serialize(resource);
        var request = _requestBuilder.BuildCreateRequest(resource, json);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return await _responseHandler
            .HandleRequiredResourceAsync<TResource>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResource> UpdateAsync<TResource>(
        TResource resource,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(resource);

        ValidateResourceBeforeSend(resource);

        var json = _serializer.Serialize(resource);
        var request = _requestBuilder.BuildUpdateRequest(resource, json);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return await _responseHandler
            .HandleRequiredResourceAsync<TResource>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Bundle> SearchAsync<TResource>(
        string query,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        var request = _requestBuilder.BuildSearchRequest<TResource>(query);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return await _responseHandler.HandleBundleAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Bundle> SearchAsync<TResource>(
        FhirSearchQuery query,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = _requestBuilder.BuildSearchRequest<TResource>(query);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return await _responseHandler.HandleBundleAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await _authProvider.ApplyAsync(request, cancellationToken).ConfigureAwait(false);

        return await _httpSender.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void ValidateResourceBeforeSend(Resource resource)
    {
        if (!_validateBeforeSend)
        {
            return;
        }

        var result = _validator.Validate(resource);
        if (result.IsValid)
        {
            return;
        }

        throw new FhirValidationException(result);
    }

    private static HttpClient ConfigureHttpClient(HttpClient httpClient, FhirClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "FHIR client timeout must be greater than zero.");
        }

        httpClient.Timeout = options.Timeout;
        return httpClient;
    }
}
