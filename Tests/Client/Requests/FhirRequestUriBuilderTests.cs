namespace MyFhirSdk.Tests.Client.Requests;

public sealed class FhirRequestUriBuilderTests
{
    [Fact]
    public void BuildResourceTypeUriPreservesBasePath()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/base/fhir"));

        var uri = builder.BuildResourceTypeUri("Patient");

        Assert.Equal("https://example.org/base/fhir/Patient", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildResourceTypeUriHandlesTrailingSlash()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir/"));

        var uri = builder.BuildResourceTypeUri("Patient");

        Assert.Equal("https://example.org/fhir/Patient", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildResourceInstanceUriEncodesResourceId()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir"));

        var uri = builder.BuildResourceInstanceUri("Patient", "a/b");

        Assert.Equal("https://example.org/fhir/Patient/a%2Fb", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildSearchUriTrimsLeadingQuestionMark()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir"));

        var uri = builder.BuildSearchUri("Patient", "?name=John");

        Assert.Equal("https://example.org/fhir/Patient?name=John", uri.AbsoluteUri);
    }
}
