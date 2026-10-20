using System.Net;
using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Client;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Task = System.Threading.Tasks.Task;

var tasks = new List<Task<object>>();
#if USE_R4
tasks.Add(Exercise<MyFhirSdk.R4.Resources.Bundle, MyFhirSdk.R4.Resources.OperationOutcome, MyFhirSdk.R4.Resources.Patient>(
    new(MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Metadata, MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Primitives,
        MyFhirSdk.ModelMetadata.R4.R4ModelProvider.Validation), "42"));
#endif
#if USE_R5
tasks.Add(Exercise<MyFhirSdk.Resources.Bundle, MyFhirSdk.Resources.OperationOutcome, MyFhirSdk.Resources.Patient>(
    MyFhirSdk.ModelMetadata.R5.R5ModelContext.Default, "\"42\""));
#endif
var releases = await Task.WhenAll(tasks);
var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name!.StartsWith("MyFhirSdk", StringComparison.Ordinal))
    .OrderBy(a => a.GetName().Name, StringComparer.Ordinal).Select(a => new { name = a.GetName().Name, identity = a.FullName, path = a.Location }).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { status = "Passed", releases, assemblies }));

static async Task<object> Exercise<TBundle, TOutcome, TPatient>(FhirModelContext context, string size)
    where TBundle : Resource where TOutcome : Resource where TPatient : Resource
{
    var json = "{\"resourceType\":\"Patient\",\"id\":\"delivery\",\"photo\":[{\"size\":" + size + "}]}";
    var parser = new FhirJsonParser(context); var serializer = new FhirJsonSerializer(context);
    var value = parser.Parse<TPatient>(json);
    if (!new FhirValidator(context).Validate(value).IsValid || serializer.Serialize(value) != json)
        throw new InvalidOperationException("Version-specific model round trip or validation failed.");
    using var http = new HttpClient(new Handler(json));
    var client = new FhirClient<TBundle, TOutcome>(http, context,
        new FhirClientOptions { BaseAddress = new Uri("https://delivery.invalid/fhir/") });
    var read = await client.ReadAsync<TPatient>("delivery");
    if (read is null || serializer.Serialize(read) != json) throw new InvalidOperationException("Typed HTTP read failed.");
    return new { fhirVersion = context.Identity.FhirVersion, modelAssembly = typeof(TPatient).Assembly.GetName().Name,
        roundTrip = true, validation = true, typedClient = true };
}

sealed class Handler(string json) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != "/fhir/Patient/delivery")
            throw new InvalidOperationException("Unexpected delivery request.");
        return new(HttpStatusCode.OK) { Content = new StringContent(json) };
    }
}
