using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Exceptions;
using MyFhirSdk.Client.Http;
using MyFhirSdk.Client.Requests;
using MyFhirSdk.Client.Responses;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Client;

/// <summary>A REST client whose models, parser, serializer and validator share one immutable context.</summary>
public sealed class FhirClient<TBundle, TOperationOutcome> : IFhirClient<TBundle, TOperationOutcome>
    where TBundle : Resource where TOperationOutcome : Resource
{
    private readonly FhirClientExecutor _executor;
    private readonly FhirRequestBuilder _requests;
    private readonly FhirResponseProcessor _responses;

    public FhirClient(HttpClient httpClient, FhirModelContext context, FhirClientOptions options, IAuthProvider? authProvider = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(options);
        var resolver = new FhirResourceTypeResolver(context);
        if (resolver.GetResourceType<TBundle>() != "Bundle" || resolver.GetResourceType<TOperationOutcome>() != "OperationOutcome")
            throw new ArgumentException("Client type arguments must be this context's Bundle and OperationOutcome.");
        _requests = new(resolver, new FhirRequestUriBuilder(options.BaseAddress));
        var parser = new FhirJsonParser(context);
        _responses = new(parser, (response, body) =>
        {
            TOperationOutcome? outcome = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try { outcome = parser.Parse<TOperationOutcome>(body); }
                catch (Exception ex) when (ex is not OperationCanceledException) { }
            }
            return new FhirHttpException<TOperationOutcome>(FhirClientException.FromResponse(response, body), outcome);
        });
        _executor = new(new FhirJsonSerializer(context), new FhirHttpSender(FhirClientExecutor.Configure(httpClient, options)),
            authProvider, new FhirValidator(context), options.ValidateBeforeSend);
    }

    public FhirModelContext Context { get; }

    public async Task<TResource?> ReadAsync<TResource>(string id, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildReadRequest<TResource>(id), _responses.OptionalAsync<TResource>, cancellationToken).ConfigureAwait(false);

    public async Task<TResource> CreateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildCreateRequest(resource, _executor.Serialize(resource)), _responses.RequiredAsync<TResource>, cancellationToken).ConfigureAwait(false);

    public async Task<TResource> UpdateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildUpdateRequest(resource, _executor.Serialize(resource)), _responses.RequiredAsync<TResource>, cancellationToken).ConfigureAwait(false);

    public async Task<TBundle> SearchAsync<TResource>(string query, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildSearchRequest<TResource>(query), _responses.RequiredAsync<TBundle>, cancellationToken).ConfigureAwait(false);

    public async Task<TBundle> SearchAsync<TResource>(FhirSearchQuery query, CancellationToken cancellationToken = default) where TResource : Resource
        => await _executor.SendAsync(_requests.BuildSearchRequest<TResource>(query), _responses.RequiredAsync<TBundle>, cancellationToken).ConfigureAwait(false);

    public async Task<TOperationOutcome?> DeleteAsync<TResource>(string id, CancellationToken cancellationToken = default) where TResource : Resource
    {
        var request = _requests.BuildReadRequest<TResource>(id);
        request.Method = HttpMethod.Delete;
        return await _executor.SendAsync(request, _responses.DeleteAsync<TOperationOutcome>, cancellationToken).ConfigureAwait(false);
    }
}
