using System.Text.Json;
using System.Net;
using MyFhirSdk.Client;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

var releases = new List<object>();
#if USE_R4
releases.Add(await Exercise<MyFhirSdk.R4.Resources.Bundle, MyFhirSdk.R4.Resources.OperationOutcome,
    MyFhirSdk.R4.Resources.Patient>("R4", new FhirModelContext(
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Metadata,
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Primitives,
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Validation), "42"));
#endif
#if USE_R5
releases.Add(await Exercise<MyFhirSdk.Resources.Bundle, MyFhirSdk.Resources.OperationOutcome,
    MyFhirSdk.Resources.Patient>("R5", new FhirModelContext(
        MyFhirSdk.ModelMetadata.R5.R5ModelProvider.Metadata,
        MyFhirSdk.ModelMetadata.R5.R5ModelProvider.Primitives,
        MyFhirSdk.ModelMetadata.R5.R5ModelProvider.Validation), "\"42\""));
#endif

#if USE_IG
var ig = IgProbe.Run();
#else
object? ig = null;
#endif
var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name!.StartsWith("MyFhirSdk", StringComparison.Ordinal))
    .OrderBy(a => a.GetName().Name, StringComparer.Ordinal).Select(a => new { name = a.GetName().Name, identity = a.FullName, path = a.Location,
        references = a.GetReferencedAssemblies().Select(r => r.Name).Order().ToArray() }).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { status = "Passed", releases, ig, assemblies }));

static async Task<object> Exercise<TBundle, TOutcome, TPatient>(string release, FhirModelContext context, string size)
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

    object Result(string status) => new { fhirVersion = context.Identity.FhirVersion, modelAssembly = typeof(TPatient).Assembly.GetName().Name,
        roundTrip = true, validation = true, typedClient = true,
        externalServer = new { status, operation = "GET metadata; GET Patient/{id}", versionChecked = status == "Passed" } };
    var endpoint = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_INTEGRATION_BASE_URL")?.Trim();
    var id = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_PATIENT_ID")?.Trim();
    if (string.IsNullOrEmpty(endpoint) && string.IsNullOrEmpty(id))
    {
        Console.WriteLine(release + ": external server read skipped (no endpoint/Patient ID supplied).");
        return Result("NotRun");
    }
    if (string.IsNullOrEmpty(id) || id.Length > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.')) ||
        !Uri.TryCreate(endpoint, UriKind.Absolute, out var baseAddress) ||
        baseAddress.Scheme is not ("http" or "https") || baseAddress.UserInfo.Length != 0 ||
        baseAddress.Query.Length != 0 || baseAddress.Fragment.Length != 0)
        throw new ArgumentException($"Set MYFHIRSDK_{release}_INTEGRATION_BASE_URL and MYFHIRSDK_{release}_PATIENT_ID together.");

    var token = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_INTEGRATION_BEARER_TOKEN");
    IAuthProvider auth = string.IsNullOrWhiteSpace(token) ? new NoAuthProvider() : new BearerTokenAuthProvider(token);
    // Keep credentials and requests on the explicitly selected endpoint.
    using var externalHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(30) };
    var externalClient = new FhirClient<TBundle, TOutcome>(externalHttp, context,
        new FhirClientOptions { BaseAddress = baseAddress }, auth);
    TPatient? existingPatient;
    var failureStage = "MetadataRequest";
    var failureCode = "RequestFailed";
    try
    {
        var metadataUri = new Uri(baseAddress.AbsoluteUri.TrimEnd('/') + "/metadata");
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUri);
        metadataRequest.Headers.Accept.ParseAdd("application/fhir+json");
        await auth.ApplyAsync(metadataRequest);
        using var metadataResponse = await externalHttp.SendAsync(metadataRequest);
        metadataResponse.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync());
        failureStage = "MetadataVersion";
        failureCode = "VersionMismatch";
        if (metadata.RootElement.GetProperty("resourceType").GetString() != "CapabilityStatement" ||
            metadata.RootElement.GetProperty("fhirVersion").GetString() != context.Identity.FhirVersion)
            throw new InvalidOperationException("Server FHIR version differs from the selected context.");
        failureStage = "PatientRead";
        failureCode = "RequestFailed";
        existingPatient = await externalClient.ReadAsync<TPatient>(id);
        failureStage = "PatientIdentity";
        failureCode = existingPatient is null ? "PatientMissing" : "PatientIdMismatch";
        if (existingPatient?.Id != id)
            throw new InvalidOperationException("External read returned an unexpected Patient ID.");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("D5_SERVER_FAILURE " + JsonSerializer.Serialize(new { stage = failureStage, code = failureCode }));
        // Keep remote error bodies and request details out of example console logs.
        throw new InvalidOperationException(release + " external read failed (" + error.GetType().Name + ").");
    }
    if (existingPatient is null) throw new InvalidOperationException("External Patient read returned no resource.");
    // Do not print Patient content, endpoint, ID or credentials.
    Console.WriteLine(release + ": external server read passed.");
    return Result("Passed");
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
