namespace MyFhirSdk.Tests.Client.Requests;

public static class FhirRequestBuilderTests
{
    public static void BuildReadRequestCreatesGetResourceInstanceRequest()
    {
        var builder = CreateBuilder();

        using var request = builder.BuildReadRequest<Patient>("123");

        TestAssert.AreEqual(HttpMethod.Get, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient/123", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
    }

    public static async Task BuildCreateRequestCreatesPostResourceTypeRequest()
    {
        var builder = CreateBuilder();
        var patient = new Patient();

        using var request = builder.BuildCreateRequest(patient, "{\"resourceType\":\"Patient\"}");

        TestAssert.AreEqual(HttpMethod.Post, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
        AssertPrefersReturnRepresentation(request);
        TestAssert.AreEqual(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        TestAssert.AreEqual("{\"resourceType\":\"Patient\"}", await request.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static async Task BuildUpdateRequestCreatesPutResourceInstanceRequest()
    {
        var builder = CreateBuilder();
        var patient = new Patient { Id = "abc" };

        using var request = builder.BuildUpdateRequest(patient, "{\"resourceType\":\"Patient\",\"id\":\"abc\"}");

        TestAssert.AreEqual(HttpMethod.Put, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient/abc", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
        AssertPrefersReturnRepresentation(request);
        TestAssert.AreEqual(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        TestAssert.AreEqual("{\"resourceType\":\"Patient\",\"id\":\"abc\"}", await request.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static void BuildUpdateRequestRequiresResourceId()
    {
        var builder = CreateBuilder();
        var patient = new Patient();

        TestAssert.Throws<ArgumentException>(() => builder.BuildUpdateRequest(patient, "{}"));
    }

    public static void BuildSearchRequestCreatesGetSearchRequestForRawQuery()
    {
        var builder = CreateBuilder();

        using var request = builder.BuildSearchRequest<Patient>("name=John");

        TestAssert.AreEqual(HttpMethod.Get, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient?name=John", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
    }

    public static void BuildSearchRequestCreatesGetSearchRequestForStructuredQuery()
    {
        var builder = CreateBuilder();
        var query = FhirSearchQuery.Create()
            .Where("name", "John Smith")
            .Count(20);

        using var request = builder.BuildSearchRequest<Patient>(query);

        TestAssert.AreEqual(HttpMethod.Get, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient?name=John%20Smith&_count=20", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
    }

    private static FhirRequestBuilder CreateBuilder()
    {
        return new FhirRequestBuilder(
            new FhirResourceTypeResolver(),
            new FhirRequestUriBuilder(new Uri("https://example.org/fhir")));
    }

    private static void AssertAcceptsFhirJson(HttpRequestMessage request)
    {
        TestAssert.IsTrue(
            request.Headers.Accept.Any(value => value.MediaType == FhirHttpConstants.FhirJsonMediaType),
            "Expected request to accept FHIR JSON.");
    }

    private static void AssertPrefersReturnRepresentation(HttpRequestMessage request)
    {
        TestAssert.IsTrue(
            request.Headers.TryGetValues(FhirHttpConstants.PreferHeaderName, out var values)
            && values.Contains(FhirHttpConstants.PreferReturnRepresentation),
            "Expected request to prefer return=representation.");
    }
}
