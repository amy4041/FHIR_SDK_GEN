namespace MyFhirSdk.Tests.Client.Requests;

public sealed class FhirResourceTypeResolverTests
{
    [Fact]
    public void GetResourceTypeFromGenericType()
    {
        var resolver = new FhirResourceTypeResolver();

        var resourceType = resolver.GetResourceType<Patient>();

        Assert.Equal("Patient", resourceType);
    }

    [Fact]
    public void GetResourceTypeFromResourceInstance()
    {
        var resolver = new FhirResourceTypeResolver();

        var resourceType = resolver.GetResourceType(new Bundle());

        Assert.Equal("Bundle", resourceType);
    }
}
