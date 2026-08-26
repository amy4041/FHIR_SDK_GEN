using System.Reflection;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

public sealed class GeneratedPrimitiveIntegrationTests
{
    [Fact]
    public void DefaultRegistryUsesGeneratedCompositionWithoutHandwrittenFallback()
    {
        var registryType = typeof(PrimitiveRegistry);
        const BindingFlags privateStatic =
            BindingFlags.NonPublic | BindingFlags.Static;

        Assert.NotNull(registryType.GetMethod(
            "AddGeneratedDefinitions",
            privateStatic));
        Assert.Null(registryType.GetMethod(
            "AddHandwrittenDefinitions",
            privateStatic));
        Assert.Equal(17, PrimitiveRegistry.Default.Definitions.Count);
        Assert.Equal(
            PrimitiveRegistry.Default.Definitions.Count,
            PrimitiveRegistry.Default.Definitions
                .Select(definition => definition.FhirTypeName)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            PrimitiveRegistry.Default.Definitions.Count,
            PrimitiveRegistry.Default.Definitions
                .Select(definition => definition.PrimitiveType)
                .Distinct()
                .Count());
    }
}
