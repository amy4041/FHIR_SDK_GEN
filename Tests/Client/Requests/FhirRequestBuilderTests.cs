namespace MyFhirSdk.Tests.Client.Requests;

public sealed class FhirRequestBuilderTests
{
    [Fact]
    public void BuildReadRequestCreatesGetResourceInstanceRequest()
    {
        var builder = CreateBuilder();

        using var request = builder.BuildReadRequest<Patient>("123");

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.org/fhir/Patient/123", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task BuildCreateRequestCreatesPostResourceTypeRequest()
    {
        var builder = CreateBuilder();
        var patient = new Patient();

        using var request = builder.BuildCreateRequest(patient, "{\"resourceType\":\"Patient\"}");

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.org/fhir/Patient", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
        AssertPrefersReturnRepresentation(request);
        Assert.Equal(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"resourceType\":\"Patient\"}", await request.Content.ReadAsStringAsync());
    }

    [Fact]
    public async global::System.Threading.Tasks.Task BuildUpdateRequestCreatesPutResourceInstanceRequest()
    {
        var builder = CreateBuilder();
        var patient = new Patient { Id = "abc" };

        using var request = builder.BuildUpdateRequest(patient, "{\"resourceType\":\"Patient\",\"id\":\"abc\"}");

        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://example.org/fhir/Patient/abc", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
        AssertPrefersReturnRepresentation(request);
        Assert.Equal(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"resourceType\":\"Patient\",\"id\":\"abc\"}", await request.Content.ReadAsStringAsync());
    }

    [Fact]
    public void BuildUpdateRequestRequiresResourceId()
    {
        var builder = CreateBuilder();
        var patient = new Patient();

        Assert.Throws<ArgumentException>(() => builder.BuildUpdateRequest(patient, "{}"));
    }

    [Fact]
    public void BuildSearchRequestCreatesGetSearchRequestForRawQuery()
    {
        var builder = CreateBuilder();

        using var request = builder.BuildSearchRequest<Patient>("name=John");

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.org/fhir/Patient?name=John", request.RequestUri!.AbsoluteUri);
        AssertAcceptsFhirJson(request);
    }

    [Fact]
    public void BuildSearchRequestCreatesGetSearchRequestForStructuredQuery()
    {
        var builder = CreateBuilder();
        var query = FhirSearchQuery.Create()
            .Where("name", "John Smith")
            .Count(20);

        using var request = builder.BuildSearchRequest<Patient>(query);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.org/fhir/Patient?name=John%20Smith&_count=20", request.RequestUri!.AbsoluteUri);
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
        Assert.True(
            request.Headers.Accept.Any(value => value.MediaType == FhirHttpConstants.FhirJsonMediaType),
            "Expected request to accept FHIR JSON.");
    }

    private static void AssertPrefersReturnRepresentation(HttpRequestMessage request)
    {
        Assert.True(
            request.Headers.TryGetValues(FhirHttpConstants.PreferHeaderName, out var values)
            && values.Contains(FhirHttpConstants.PreferReturnRepresentation),
            "Expected request to prefer return=representation.");
    }
}
