using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using MyFhirSdk.Client;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Exceptions;
using MyFhirSdk.Client.Requests;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using Xunit;
using R4R = MyFhirSdk.R4.Resources;
using R5R = MyFhirSdk.Resources;

namespace MyFhirSdk.Tests.FhirVersions.V6;

public sealed class R4ClientTests : ClientContractTests<R4R.Bundle, R4R.OperationOutcome, R4R.Patient>
{
    protected override FhirModelContext Context => new(R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);
    protected override Resource FirstEntry(R4R.Bundle bundle) => bundle.Entry.First().Resource!;
}
public sealed class R5ClientTests : ClientContractTests<R5R.Bundle, R5R.OperationOutcome, R5R.Patient>
{
    protected override FhirModelContext Context => R5ModelContext.Default;
    protected override Resource FirstEntry(R5R.Bundle bundle) => bundle.Entry.First().Resource!;
}

public abstract class ClientContractTests<TBundle, TOutcome, TPatient>
    where TBundle : Resource where TOutcome : Resource where TPatient : Resource
{
    protected abstract FhirModelContext Context { get; }
    protected abstract Resource FirstEntry(TBundle bundle);
    private const string Outcome = """{"resourceType":"OperationOutcome","issue":[{"severity":"error","code":"invalid","diagnostics":"bad request"}]}""";
    private string Photo => Context.Identity.FhirVersion == "4.0.1" ? "42" : "\"42\"";
    private TPatient Patient(string fields = "") => new FhirJsonParser(Context).Parse<TPatient>("{\"resourceType\":\"Patient\",\"id\":\"p\"" + fields + "}");
    private FhirClient<TBundle, TOutcome> Client(HttpClient http, bool validate = true, IAuthProvider? auth = null)
        => new(http, Context, new FhirClientOptions { BaseAddress = new("https://example.test/fhir"), ValidateBeforeSend = validate }, auth);

    [Fact]
    public async Task CrudSearchUsesTheBoundContextAndHttpHeaders()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var body = "{\"resourceType\":\"Patient\",\"id\":\"p\",\"photo\":[{\"size\":" + Photo + "}]}";
        handler.Enqueue(HttpStatusCode.Created, body); handler.Enqueue(HttpStatusCode.OK, body);
        handler.Enqueue(HttpStatusCode.OK, body);
        handler.Enqueue(HttpStatusCode.OK, "{\"resourceType\":\"Bundle\",\"type\":\"searchset\",\"entry\":[{\"resource\":" + body + "}]}");
        handler.Enqueue(HttpStatusCode.NoContent, "");
        var client = Client(http, auth: new BearerTokenAuthProvider("test-token"));
        var created = await client.CreateAsync(Patient(",\"photo\":[{\"size\":" + Photo + "}]"));
        Assert.IsType<TPatient>(await client.ReadAsync<TPatient>("p"));
        Assert.IsType<TPatient>(await client.UpdateAsync(created));
        TBundle bundle = await client.SearchAsync<TPatient>(FhirSearchQuery.Create().Where("identifier", "urn:test|a b"));
        Assert.IsType<TPatient>(FirstEntry(bundle));
        Assert.Null(await client.DeleteAsync<TPatient>("p"));
        var requests = handler.Requests.ToArray();
        Assert.Equal(new[] { "POST", "GET", "PUT", "GET", "DELETE" }, requests.Select(r => r.Method));
        Assert.All(requests, r => { Assert.Contains("application/fhir+json", r.Accept); Assert.Equal("Bearer test-token", r.Authorization); });
        Assert.Equal("https://example.test/fhir/Patient", requests[0].Uri);
        Assert.Equal("https://example.test/fhir/Patient/p", requests[1].Uri);
        Assert.Contains("identifier=urn%3Atest%7Ca%20b", requests[3].Uri);
        foreach (var r in new[] { requests[0], requests[2] })
        {
            Assert.Equal("application/fhir+json", r.ContentType); Assert.Equal("return=representation", r.Prefer);
            using var json = JsonDocument.Parse(r.Body!);
            Assert.Equal(Photo, json.RootElement.GetProperty("photo")[0].GetProperty("size").GetRawText());
        }
        Assert.False(typeof(FhirClient<TBundle, TOutcome>).GetProperty("Context")!.CanWrite);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SearchRawAndStructuredQueriesReturnVersionOwnedBundleEntries(bool structured)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.OK, """{"resourceType":"Bundle","type":"searchset","entry":[{"resource":{"resourceType":"Patient","id":"one"}},{"resource":{"resourceType":"OperationOutcome","issue":[{"severity":"information","code":"informational"}]}}]}""");
        var client = Client(http);
        var bundle = structured ? await client.SearchAsync<TPatient>(FhirSearchQuery.Create().Where("name", "Ada"))
            : await client.SearchAsync<TPatient>("?name=Ada");
        Assert.IsType<TPatient>(FirstEntry(bundle));
        var json = new FhirJsonSerializer(client.Context).Serialize(bundle);
        Assert.Contains("OperationOutcome", json);
        Assert.EndsWith("Patient?name=Ada", handler.Requests.Single().Uri);
    }

    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(422)] [InlineData(500)]
    public async Task HttpErrorsExposeVersionOwnedOperationOutcomeAndRequestDetails(int status)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue((HttpStatusCode)status, Outcome);
        var ex = await Assert.ThrowsAsync<FhirHttpException<TOutcome>>(() => Client(http).CreateAsync(Patient()));
        Assert.Equal((HttpStatusCode)status, ex.StatusCode); Assert.Equal(Outcome, ex.ResponseBody);
        Assert.Equal(HttpMethod.Post, ex.Method); Assert.Equal("https://example.test/fhir/Patient", ex.RequestUri!.AbsoluteUri);
        Assert.IsType<TOutcome>(ex.OperationOutcome); Assert.IsType<FhirHttpException>(ex.InnerException);
    }

    [Theory]
    [InlineData("")] [InlineData("<html>failure</html>")]
    [InlineData("{\"resourceType\":\"FutureResource\"}")]
    public async Task MalformedErrorBodyKeepsHttpFailureAndRawBody(string body)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.BadGateway, body);
        var ex = await Assert.ThrowsAsync<FhirHttpException<TOutcome>>(() => Client(http).ReadAsync<TPatient>("p"));
        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode); Assert.Equal(body, ex.ResponseBody); Assert.Null(ex.OperationOutcome);
    }

    [Fact]
    public async Task Read404ReturnsNullAndDelete200ReturnsTypedOutcome()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.NotFound, Outcome); handler.Enqueue(HttpStatusCode.OK, Outcome);
        var client = Client(http);
        Assert.Null(await client.ReadAsync<TPatient>("missing"));
        Assert.IsType<TOutcome>(await client.DeleteAsync<TPatient>("p"));
    }

    [Fact]
    public async Task DeleteErrorsRetainHttpStatusAndUnexpectedSuccessBodiesAreRejected()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.NotFound, Outcome);
        handler.Enqueue(HttpStatusCode.OK, "{\"resourceType\":\"Patient\"}");
        var client = Client(http);
        var error = await Assert.ThrowsAsync<FhirHttpException<TOutcome>>(() => client.DeleteAsync<TPatient>("missing"));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode); Assert.IsType<TOutcome>(error.OperationOutcome);
        await Assert.ThrowsAsync<FhirInvalidResponseException>(() => client.DeleteAsync<TPatient>("p"));
    }

    [Theory]
    [InlineData("")] [InlineData("{\"resourceType\":\"Patient\"}")]
    public async Task SearchRejectsEmptyOrNonBundleSuccessBodies(string body)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.OK, body);
        await Assert.ThrowsAsync<FhirInvalidResponseException>(() => Client(http).SearchAsync<TPatient>(""));
    }

    [Fact]
    public async Task DefaultValidationHonorsTheReleaseSpecificLexicalRules()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var patient = Patient(",\"extension\":[{\"url\":\"urn:time\",\"valueTime\":\"10:20:30.1234567890\"}]");
        handler.Enqueue(HttpStatusCode.Created, new FhirJsonSerializer(Context).Serialize(patient));
        var client = Client(http);
        if (Context.Identity.FhirVersion == "4.0.1") Assert.IsType<TPatient>(await client.CreateAsync(patient));
        else
        {
            var error = await Assert.ThrowsAsync<FhirValidationException>(() => client.CreateAsync(patient));
            Assert.Contains(error.Result.Issues, i => i.Code == MyFhirSdk.Validation.ValidationIssueCode.PrimitiveFormat);
            Assert.Empty(handler.Requests);
        }
    }

    [Fact]
    public async Task AuthenticationFailuresDisposeRequestsWithoutSendingHttp()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var auth = new FailingAuth();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Client(http, auth: auth).CreateAsync(Patient()));
        Assert.Empty(handler.Requests);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => auth.Request!.Content!.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")] [InlineData("{bad")]
    [InlineData("{\"resourceType\":\"OperationOutcome\"}")]
    [InlineData("{\"resourceType\":\"FutureResource\"}")]
    public async Task InvalidSuccessResponsesAreWrappedAsInvalidResponse(string body)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.OK, body);
        await Assert.ThrowsAsync<FhirInvalidResponseException>(() => Client(http).ReadAsync<TPatient>("p"));
    }

    [Fact]
    public async Task UnknownContainedResourceInSearchIsRejected()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.OK, """{"resourceType":"Bundle","type":"searchset","entry":[{"resource":{"resourceType":"FutureResource"}}]}""");
        await Assert.ThrowsAsync<FhirInvalidResponseException>(() => Client(http).SearchAsync<TPatient>(""));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DefaultValidationUsesContextAndCanBeDisabled(bool validate)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.Created, "{\"resourceType\":\"Patient\",\"id\":\"p\"}");
        var patient = Patient(",\"birthDate\":\"2024-02-30\"");
        if (validate)
        {
            var ex = await Assert.ThrowsAsync<FhirValidationException>(() => Client(http).CreateAsync(patient));
            Assert.Contains(ex.Result.Issues, i => i.Path == "Patient.birthDate"); Assert.Empty(handler.Requests);
        }
        else Assert.IsType<TPatient>(await Client(http, false).CreateAsync(patient));
    }

    [Theory]
    [InlineData("root")] [InlineData("contained")] [InlineData("extension")]
    public async Task ForeignModelsAreRejectedBeforeHttpEvenWithoutValidation(string location)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var foreign = Context.Identity.FhirVersion == "4.0.1" ? (Resource)new R5R.Patient() : new R4R.Patient();
        Resource resource = foreign;
        if (location != "root")
        {
            var patient = (DomainResource)(Resource)Patient();
            if (location == "contained") patient.Contained.Add(foreign);
            else patient.Extension.Add(new Extension { Url = "urn:test", Value = Context.Identity.FhirVersion == "4.0.1"
                ? new MyFhirSdk.Primitives.FhirString("foreign") : new MyFhirSdk.R4.Primitives.FhirString("foreign") });
            resource = patient;
        }
        await Assert.ThrowsAsync<FhirSdkException>(() => Client(http, false).CreateAsync(resource));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ForeignReadAndAbstractSearchTypesAreRejectedBeforeHttp()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var client = Client(http);
        if (Context.Identity.FhirVersion == "4.0.1")
            await Assert.ThrowsAsync<FhirSdkException>(() => client.ReadAsync<R5R.Patient>("p"));
        else await Assert.ThrowsAsync<FhirSdkException>(() => client.ReadAsync<R4R.Patient>("p"));
        await Assert.ThrowsAsync<FhirSdkException>(() => client.SearchAsync<Resource>(""));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("read")] [InlineData("update")] [InlineData("delete")]
    public async Task MissingIdsAreRejectedBeforeHttp(string operation)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var client = Client(http);
        if (operation == "read") await Assert.ThrowsAsync<ArgumentException>(() => client.ReadAsync<TPatient>(""));
        else if (operation == "delete") await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteAsync<TPatient>(""));
        else { var patient = Patient(); patient.Id = null; await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateAsync(patient)); }
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(".", "read", false)] [InlineData("..", "read", false)]
    [InlineData(".", "update", false)] [InlineData("..", "update", false)]
    [InlineData(".", "delete", false)] [InlineData("..", "delete", false)]
    [InlineData(".", "read", true)] [InlineData("..", "read", true)]
    [InlineData(".", "update", true)] [InlineData("..", "update", true)]
    [InlineData(".", "delete", true)] [InlineData("..", "delete", true)]
    public async Task DotSegmentIdsAreRejectedBeforeHttp(string id, string operation, bool validate)
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var client = Client(http, validate);
        ArgumentException error;
        if (operation == "read") error = await Assert.ThrowsAsync<ArgumentException>(() => client.ReadAsync<TPatient>(id));
        else if (operation == "delete") error = await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteAsync<TPatient>(id));
        else
        {
            var patient = Patient(); patient.Id = id;
            error = await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateAsync(patient));
        }
        Assert.Equal("id", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CancellationPropagatesWithoutBeingWrapped()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var client = Client(http);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadAsync<TPatient>("p", cancelled.Token));
        Assert.Empty(handler.Requests);
        handler.OnSend = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new(HttpStatusCode.OK); };
        using var pending = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadAsync<TPatient>("p", pending.Token));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResponseAndRequestContentAreDisposedAfterParsing()
    {
        using var handler = new QueueHandler(); using var http = new HttpClient(handler);
        var content = new TrackingContent("{\"resourceType\":\"Patient\",\"id\":\"p\"}");
        handler.OnSend = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = content });
        await Client(http).CreateAsync(Patient());
        Assert.True(content.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => handler.LastRequest!.Content!.ReadAsStringAsync());
    }

    internal sealed record RequestRecord(string Method, string Uri, string Accept, string? Authorization, string Prefer, string? ContentType, string? Body);
    internal sealed class QueueHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<(HttpStatusCode Status, string Body)> _responses = new();
        internal readonly ConcurrentQueue<RequestRecord> Requests = new();
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnSend;
        internal HttpRequestMessage? LastRequest;
        internal void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Enqueue(new(request.Method.Method, request.RequestUri!.AbsoluteUri, request.Headers.Accept.ToString(),
                request.Headers.Authorization?.ToString(), string.Join(",", request.Headers.GetValuesOrEmpty("Prefer")),
                request.Content?.Headers.ContentType?.MediaType, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            if (OnSend is not null) return await OnSend(request, cancellationToken);
            if (!_responses.TryDequeue(out var next)) throw new InvalidOperationException("Unexpected HTTP request");
            return new(next.Status) { Content = new StringContent(next.Body) };
        }
    }
    private sealed class TrackingContent(string value) : StringContent(value)
    {
        internal bool Disposed;
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class FailingAuth : IAuthProvider
    {
        internal HttpRequestMessage? Request;
        public Task ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            Request = request;
            throw new InvalidOperationException("Authentication failed");
        }
    }
}

internal static class HeaderExtensions
{
    internal static IEnumerable<string> GetValuesOrEmpty(this System.Net.Http.Headers.HttpHeaders headers, string name)
        => headers.TryGetValues(name, out var values) ? values : [];
}
