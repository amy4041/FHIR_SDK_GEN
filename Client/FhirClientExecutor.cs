using MyFhirSdk.Client.Abstractions;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Exceptions;
using MyFhirSdk.Core;
using MyFhirSdk.Serialization;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Client;

// Shared HTTP lifecycle for legacy and context-bound clients. No version-specific model types.
internal sealed class FhirClientExecutor(IFhirSerializer serializer, IFhirHttpSender sender,
    IAuthProvider? auth, IFhirValidator? validator, bool validateBeforeSend)
{
    private readonly IFhirSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    private readonly IFhirHttpSender _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    private readonly IAuthProvider _auth = auth ?? NoAuthProvider.Instance;
    private readonly IFhirValidator _validator = validator ?? new FhirValidator();

    internal string Serialize(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (validateBeforeSend)
        {
            var result = _validator.Validate(resource);
            if (!result.IsValid) throw new FhirValidationException(result);
        }
        return _serializer.Serialize(resource);
    }

    internal async Task<T> SendAsync<T>(HttpRequestMessage request,
        Func<HttpResponseMessage, CancellationToken, Task<T>> handle, CancellationToken cancellationToken)
    {
        using (request)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _auth.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await _sender.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.RequestMessage ??= request;
            return await handle(response, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static HttpClient Configure(HttpClient httpClient, FhirClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "FHIR client timeout must be greater than zero.");
        httpClient.Timeout = options.Timeout;
        return httpClient;
    }
}
