using System.Net;
using MyFhirSdk.Client;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

#if USE_R4
await Exercise<MyFhirSdk.R4.Resources.Bundle, MyFhirSdk.R4.Resources.OperationOutcome,
    MyFhirSdk.R4.Resources.Patient>("R4", new FhirModelContext(
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Metadata,
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Primitives,
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Validation), "42");
#endif
#if USE_R5
await Exercise<MyFhirSdk.Resources.Bundle, MyFhirSdk.Resources.OperationOutcome,
    MyFhirSdk.Resources.Patient>("R5", MyFhirSdk.ModelMetadata.R5.R5ModelContext.Default, "\"42\"");
#endif

static async Task Exercise<TBundle, TOutcome, TPatient>(string release, FhirModelContext context, string size)
    where TBundle : Resource where TOutcome : Resource where TPatient : Resource
{
    var json = "{\"resourceType\":\"Patient\",\"id\":\"example\",\"photo\":[{\"size\":" + size + "}]}";
    var parser = new FhirJsonParser(context);
    var serializer = new FhirJsonSerializer(context);
    var patient = parser.Parse<TPatient>(json);
    if (!new FhirValidator(context).Validate(patient).IsValid || serializer.Serialize(patient) != json)
        throw new InvalidOperationException(release + " model round trip failed.");

    // This controlled read is available without a server or pre-existing Patient.
    using var localHttp = new HttpClient(new ExampleHandler(json));
    var localClient = new FhirClient<TBundle, TOutcome>(localHttp, context,
        new FhirClientOptions { BaseAddress = new Uri("https://example.invalid/fhir/") });
    var localPatient = await localClient.ReadAsync<TPatient>("example");
    if (localPatient is null || serializer.Serialize(localPatient) != json)
        throw new InvalidOperationException(release + " controlled HTTP read failed.");
    Console.WriteLine(release + ": JSON, validation and controlled HTTP read passed.");

    var endpoint = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_INTEGRATION_BASE_URL")?.Trim();
    var id = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_PATIENT_ID")?.Trim();
    if (string.IsNullOrEmpty(endpoint) && string.IsNullOrEmpty(id))
    {
        Console.WriteLine(release + ": external server read skipped (no endpoint/Patient ID supplied).");
        return;
    }
    if (string.IsNullOrEmpty(id) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var baseAddress) ||
        baseAddress.Scheme is not ("http" or "https") || baseAddress.UserInfo.Length != 0 ||
        baseAddress.Query.Length != 0 || baseAddress.Fragment.Length != 0)
        throw new ArgumentException($"Set MYFHIRSDK_{release}_INTEGRATION_BASE_URL and MYFHIRSDK_{release}_PATIENT_ID together.");

    var token = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_INTEGRATION_BEARER_TOKEN");
    IAuthProvider auth = string.IsNullOrWhiteSpace(token) ? new NoAuthProvider() : new BearerTokenAuthProvider(token);
    using var externalHttp = new HttpClient();
    var externalClient = new FhirClient<TBundle, TOutcome>(externalHttp, context,
        new FhirClientOptions { BaseAddress = baseAddress }, auth);
    TPatient? existingPatient;
    try { existingPatient = await externalClient.ReadAsync<TPatient>(id); }
    catch (Exception error)
    {
        // Keep remote error bodies and request details out of example console logs.
        throw new InvalidOperationException(release + " external read failed (" + error.GetType().Name + ").");
    }
    if (existingPatient is null) throw new InvalidOperationException("External Patient read returned no resource.");
    // Do not print Patient content, endpoint, ID or credentials.
    Console.WriteLine(release + ": external server read passed.");
}

sealed class ExampleHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != "/fhir/Patient/example")
            throw new InvalidOperationException("Unexpected example request.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
