using System.Text;
using System.Text.Json.Nodes;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;

namespace MyFhirSdk.Tests.Client;

public sealed class KernelClientMigrationTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task RealClientEnginesReadCreateUpdateAndSearchSplitModels()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var patient = await client.ReadAsync<Patient>("p1");
        Assert.NotNull(patient);
        Assert.NotEqual(typeof(Resource).Assembly, patient.GetType().Assembly);
        Assert.False(patient.Active!.Value);
        Assert.Equal("active-id", patient.Active.Id);
        ((IPrimitiveValueAccessor)patient.Active).SetUntypedValue(true);
        var created = await client.CreateAsync(patient);
        Assert.True(created.Active!.Value);
        Assert.Equal("active-id", created.Active.Id);
        var updated = await client.UpdateAsync(created);
        Assert.True(updated.Active!.Value);
        Assert.Equal("active-id", updated.Active.Id);
        var bundle = await client.SearchAsync<Patient>(FhirSearchQuery.Create().Where("_id", "p1"));
        Assert.Equal("p1", updated.Id);
        var result = Assert.IsType<Patient>(Assert.Single(bundle.Entry).Resource);
        Assert.False(result.Active!.Value);
        Assert.Equal("active-id", result.Active.Id);
        Assert.Equal(new[] { "GET", "POST", "PUT", "GET" }, handler.Requests.Select(request => request.Method));
        Assert.Equal("https://example.test/fhir/Patient/p1", handler.Requests[0].Uri);
        Assert.Equal("https://example.test/fhir/Patient", handler.Requests[1].Uri);
        Assert.Equal("https://example.test/fhir/Patient/p1", handler.Requests[2].Uri);
        Assert.Equal("https://example.test/fhir/Patient?_id=p1", handler.Requests[3].Uri);
        var writes = handler.Requests.Where(request => request.Method is "POST" or "PUT").ToArray();
        Assert.Equal(2, writes.Length);
        foreach (var request in writes)
        {
            Assert.False(string.IsNullOrWhiteSpace(request.Body), $"{request.Method} requires a JSON body.");
            var body = JsonNode.Parse(request.Body!)!;
            Assert.True(body["active"]!.GetValue<bool>());
            Assert.Equal("active-id", body["_active"]!["id"]!.GetValue<string>());
            Assert.Equal(FhirHttpConstants.FhirJsonMediaType, request.MediaType);
        }
    }

    [Fact]
    public async global::System.Threading.Tasks.Task DefaultValidatorRejectsKernelIdAndPrimitiveFormatBeforeHttp()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<FhirValidationException>(() => CreateClient(http).CreateAsync(
            new Patient { Id = "bad/id", BirthDate = new FhirDate("not-a-date") }));
        Assert.Contains(exception.Result.Issues, issue => issue.Path == "Patient.id");
        Assert.Contains(exception.Result.Issues, issue => issue.Path == "Patient.birthDate");
        Assert.Empty(handler.Requests);
    }

    private static FhirClient CreateClient(HttpClient http) => new(http, new FhirJsonSerializer(), new FhirJsonParser(),
        new FhirClientOptions { BaseAddress = new Uri("https://example.test/fhir"), ValidateBeforeSend = true });

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Body, string? MediaType)> Requests { get; } = [];
        protected override async global::System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, body, request.Content?.Headers.ContentType?.MediaType));
            const string patient = "{\"resourceType\":\"Patient\",\"id\":\"p1\",\"active\":false,\"_active\":{\"id\":\"active-id\"}}";
            var json = request.RequestUri.Query.Length > 0
                ? "{\"resourceType\":\"Bundle\",\"type\":\"searchset\",\"entry\":[{\"resource\":" + patient + "}]}"
                : body ?? patient;
            return new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, FhirHttpConstants.FhirJsonMediaType) };
        }
    }
}
