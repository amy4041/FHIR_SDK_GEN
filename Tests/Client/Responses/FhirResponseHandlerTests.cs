namespace MyFhirSdk.Tests.Client.Responses;

public sealed class FhirResponseProcessorTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task HandleRequiredResourceAsyncParsesSuccessfulBody()
    {
        var parser = new FakeFhirParser();
        var expected = new Patient { Id = "123" };
        parser.AddResource(expected);
        var handler = new FhirResponseProcessor(parser);
        using var response = CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"123\"}");

        var actual = await handler.RequiredAsync<Patient>(response);

        Assert.Same(expected, actual);
        Assert.Equal(1, parser.ParseCallCount);
        Assert.Equal("{\"resourceType\":\"Patient\",\"id\":\"123\"}", parser.LastJson);
        Assert.Equal(typeof(Patient), parser.LastResourceType);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task HandleOptionalResourceAsyncReturnsNullForNotFound()
    {
        var parser = new FakeFhirParser();
        var handler = new FhirResponseProcessor(parser);
        using var response = CreateResponse(HttpStatusCode.NotFound, "{\"resourceType\":\"OperationOutcome\"}");

        var actual = await handler.OptionalAsync<Patient>(response);

        Assert.Null(actual);
        Assert.Equal(0, parser.ParseCallCount);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task HandleRequiredResourceAsyncRejectsEmptyBody()
    {
        var handler = new FhirResponseProcessor(new FakeFhirParser());
        using var response = CreateResponse(HttpStatusCode.OK, "");

        await Assert.ThrowsAsync<FhirInvalidResponseException>(
            () => handler.RequiredAsync<Patient>(response));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task HandleRequiredResourceAsyncThrowsHttpExceptionForNonSuccess()
    {
        var handler = new FhirResponseProcessor(new FakeFhirParser());
        using var response = CreateResponse(
            HttpStatusCode.BadRequest,
            "{\"resourceType\":\"OperationOutcome\"}",
            new HttpRequestMessage(HttpMethod.Post, "https://example.org/fhir/Patient"));

        var exception = await Assert.ThrowsAsync<FhirHttpException>(
            () => handler.RequiredAsync<Patient>(response));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("{\"resourceType\":\"OperationOutcome\"}", exception.ResponseBody);
        Assert.Equal(HttpMethod.Post, exception.Method);
        Assert.Equal("https://example.org/fhir/Patient", exception.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task HandleRequiredResourceAsyncWrapsParserFailure()
    {
        var parser = new FakeFhirParser
        {
            ExceptionToThrow = new InvalidOperationException("Parse failed.")
        };
        var handler = new FhirResponseProcessor(parser);
        using var response = CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\"}");

        var exception = await Assert.ThrowsAsync<FhirInvalidResponseException>(
            () => handler.RequiredAsync<Patient>(response));

        Assert.NotNull(exception.InnerException);
        Assert.Equal("Parse failed.", exception.InnerException!.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task HandleBundleAsyncParsesSuccessfulBundle()
    {
        var parser = new FakeFhirParser();
        var expected = new Bundle();
        parser.AddResource(expected);
        var handler = new FhirResponseProcessor(parser);
        using var response = CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Bundle\"}");

        var actual = await handler.RequiredAsync<Bundle>(response);

        Assert.Same(expected, actual);
        Assert.Equal(1, parser.ParseCallCount);
        Assert.Equal("{\"resourceType\":\"Bundle\"}", parser.LastJson);
        Assert.Equal(typeof(Bundle), parser.LastResourceType);
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode statusCode,
        string body,
        HttpRequestMessage? request = null)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
            RequestMessage = request ?? new HttpRequestMessage(HttpMethod.Get, "https://example.org/fhir/Patient/123")
        };
    }
}
