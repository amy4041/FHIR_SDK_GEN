namespace MyFhirSdk.Tests.Client.Requests;

public static class FhirResourceTypeResolverTests
{
    public static void GetResourceTypeFromGenericType()
    {
        var resolver = new FhirResourceTypeResolver();

        var resourceType = resolver.GetResourceType<Patient>();

        TestAssert.AreEqual("Patient", resourceType);
    }

    public static void GetResourceTypeFromResourceInstance()
    {
        var resolver = new FhirResourceTypeResolver();

        var resourceType = resolver.GetResourceType(new Bundle());

        TestAssert.AreEqual("Bundle", resourceType);
    }
}
