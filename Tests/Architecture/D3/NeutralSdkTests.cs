using MyFhirSdk.Client.Requests;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Profiles;

namespace MyFhirSdk.Tests.Architecture.D3;

public sealed class NeutralSdkTests
{
    [Fact]
    public void SdkAssemblyDependsOnlyOnNeutralSdkComponents()
    {
        var sdk = typeof(FhirValidator).Assembly;
        var references = sdk.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("MyFhirSdk")).Select(a => a.Name).Order().ToArray();
        Assert.Equal(new[] { "MyFhirSdk.ModelContracts", "MyFhirSdk.ModelSupport", "MyFhirSdk.Runtime" }, references);
        Assert.Empty(sdk.GetForwardedTypes());
    }

    [Fact]
    public void RetiredDefaultsClientsAndProfileOverloadsAreAbsent()
    {
        var sdk = typeof(FhirValidator).Assembly;
        foreach (var name in new[] { "MyFhirSdk.ModelMetadata.R5.R5ModelContext", "MyFhirSdk.ModelMetadata.R5.R5ModelMetadataProvider",
            "MyFhirSdk.Client.FhirClient", "MyFhirSdk.Client.Abstractions.IFhirClient",
            "MyFhirSdk.Client.Responses.FhirResponseHandler", "MyFhirSdk.Client.Abstractions.IFhirResponseHandler" })
            Assert.Null(sdk.GetType(name));
        foreach (var type in new[] { typeof(FhirJsonParser), typeof(FhirJsonSerializer), typeof(FhirValidator), typeof(FhirResourceTypeResolver) })
            Assert.Null(type.GetConstructor(Type.EmptyTypes));
        Assert.All(typeof(ProfileValidator).GetConstructors(), c => Assert.Equal(typeof(IContextualFhirValidator), c.GetParameters()[0].ParameterType));
        Assert.Null(typeof(MyFhirSdk.Primitives.PrimitiveRegistry).GetProperty("Default",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
    }
}
