using MyFhirSdk.Core;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;

namespace MyFhirSdk.Tests.Architecture;

public sealed class GeneratedModelIntegrationTests
{
    [Fact]
    public void SdkAssemblyContainsGeneratedOwnersAndNoHandwrittenEntryOwners()
    {
        var assembly = typeof(FhirObject).Assembly;

        Assert.NotNull(assembly.GetType(
            "MyFhirSdk.ModelMetadata.R5.GeneratedR5ModelMetadata"));
        Assert.NotNull(assembly.GetType(
            "MyFhirSdk.ModelMetadata.R5.GeneratedR5ValidationRules"));
        Assert.Null(assembly.GetType(
            "MyFhirSdk.ModelMetadata.R5.R5HandwrittenModelMetadataEntries"));
        Assert.Null(assembly.GetType(
            "MyFhirSdk.ModelMetadata.R5.R5ValidationRuleEntries"));
    }

    [Fact]
    public void DefaultParserUsesGeneratedFactoryForNewlyIntegratedResource()
    {
        var resource = new FhirJsonParser().Parse<Resource>(
            "{\"resourceType\":\"Account\"}");

        Assert.IsType<Account>(resource);
    }
}
