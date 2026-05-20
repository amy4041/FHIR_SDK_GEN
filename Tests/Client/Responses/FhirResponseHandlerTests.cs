namespace MyFhirSdk.Tests.Client.Responses;

public static class FhirResponseHandlerTests
{
    public static async Task HandleRequiredResourceAsyncParsesSuccessfulBody()
    {
        var parser = new FakeFhirParser();
        var expected = new Patient { Id = "123" };
        parser.AddResource(expected);
        var handler = new FhirResponseHandler(parser);
        using var response = CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"123\"}");

        var actual = await handler.HandleRequiredResourceAsync<Patient>(response).ConfigureAwait(false);

        TestAssert.AreSame(expected, actual);
        TestAssert.AreEqual(1, parser.ParseCallCount);
        TestAssert.AreEqual("{\"resourceType\":\"Patient\",\"id\":\"123\"}", parser.LastJson);
        TestAssert.AreEqual(typeof(Patient), parser.LastResourceType);
    }

    public static async Task HandleOptionalResourceAsyncReturnsNullForNotFound()
    {
        var parser = new FakeFhirParser();
        var handler = new FhirResponseHandler(parser);
        using var response = CreateResponse(HttpStatusCode.NotFound, "{\"resourceType\":\"OperationOutcome\"}");

        var actual = await handler.HandleOptionalResourceAsync<Patient>(response).ConfigureAwait(false);

        TestAssert.IsNull(actual);
        TestAssert.AreEqual(0, parser.ParseCallCount);
    }

    public static async Task HandleRequiredResourceAsyncRejectsEmptyBody()
    {
        var handler = new FhirResponseHandler(new FakeFhirParser());
        using var response = CreateResponse(HttpStatusCode.OK, "");

        await TestAssert.ThrowsAsync<FhirInvalidResponseException>(
            () => handler.HandleRequiredResourceAsync<Patient>(response)).ConfigureAwait(false);
    }

    public static async Task HandleRequiredResourceAsyncThrowsHttpExceptionForNonSuccess()
    {
        var handler = new FhirResponseHandler(new FakeFhirParser());
        using var response = CreateResponse(
            HttpStatusCode.BadRequest,
            "{\"resourceType\":\"OperationOutcome\"}",
            new HttpRequestMessage(HttpMethod.Post, "https://example.org/fhir/Patient"));

        var exception = await TestAssert.ThrowsAsync<FhirHttpException>(
            () => handler.HandleRequiredResourceAsync<Patient>(response)).ConfigureAwait(false);

        TestAssert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        TestAssert.AreEqual("{\"resourceType\":\"OperationOutcome\"}", exception.ResponseBody);
        TestAssert.AreEqual(HttpMethod.Post, exception.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient", exception.RequestUri!.AbsoluteUri);
    }

    public static async Task HandleRequiredResourceAsyncWrapsParserFailure()
    {
        var parser = new FakeFhirParser
        {
            ExceptionToThrow = new InvalidOperationException("Parse failed.")
        };
        var handler = new FhirResponseHandler(parser);
        using var response = CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\"}");

        var exception = await TestAssert.ThrowsAsync<FhirInvalidResponseException>(
            () => handler.HandleRequiredResourceAsync<Patient>(response)).ConfigureAwait(false);

        TestAssert.IsNotNull(exception.InnerException);
        TestAssert.AreEqual("Parse failed.", exception.InnerException!.Message);
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
