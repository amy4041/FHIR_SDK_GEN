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

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void BuildResourceInstanceUriRejectsDotSegments(string id)
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/base/fhir"));

        var error = Assert.Throws<ArgumentException>(() => builder.BuildResourceInstanceUri("Patient", id));

        Assert.Equal("id", error.ParamName);
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("...")]
    [InlineData("a..b")]
    public void BuildResourceInstanceUriPreservesDotsWithinIds(string id)
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/base/fhir"));

        Assert.Equal("https://example.org/base/fhir/Patient/" + id,
            builder.BuildResourceInstanceUri("Patient", id).AbsoluteUri);
    }

    [Fact]
    public void BuildSearchUriTrimsLeadingQuestionMark()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir"));

        var uri = builder.BuildSearchUri("Patient", "?name=John");

        Assert.Equal("https://example.org/fhir/Patient?name=John", uri.AbsoluteUri);
    }
}
