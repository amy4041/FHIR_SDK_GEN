namespace MyFhirSdk.Tests.Client.Requests;

public static class FhirRequestUriBuilderTests
{
    public static void BuildResourceTypeUriPreservesBasePath()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/base/fhir"));

        var uri = builder.BuildResourceTypeUri("Patient");

        TestAssert.AreEqual("https://example.org/base/fhir/Patient", uri.AbsoluteUri);
    }

    public static void BuildResourceTypeUriHandlesTrailingSlash()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir/"));

        var uri = builder.BuildResourceTypeUri("Patient");

        TestAssert.AreEqual("https://example.org/fhir/Patient", uri.AbsoluteUri);
    }

    public static void BuildResourceInstanceUriEncodesResourceId()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir"));

        var uri = builder.BuildResourceInstanceUri("Patient", "a/b");

        TestAssert.AreEqual("https://example.org/fhir/Patient/a%2Fb", uri.AbsoluteUri);
    }

    public static void BuildSearchUriTrimsLeadingQuestionMark()
    {
        var builder = new FhirRequestUriBuilder(new Uri("https://example.org/fhir"));

        var uri = builder.BuildSearchUri("Patient", "?name=John");

        TestAssert.AreEqual("https://example.org/fhir/Patient?name=John", uri.AbsoluteUri);
    }
}
