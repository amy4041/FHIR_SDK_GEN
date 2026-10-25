using System.Reflection;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

public sealed class GeneratedPrimitiveIntegrationTests
{
    [Fact]
    public void DefaultRegistryUsesGeneratedCompositionWithoutHandwrittenFallback()
    {
        var registryType = typeof(FhirString).Assembly.GetType("MyFhirSdk.Primitives.PrimitiveRegistry", throwOnError: true)!;
        const BindingFlags privateStatic =
            BindingFlags.NonPublic | BindingFlags.Static;

        Assert.NotNull(registryType.GetMethod(
            "AddGeneratedDefinitions",
            privateStatic));
        Assert.Null(registryType.GetMethod(
            "AddHandwrittenDefinitions",
            privateStatic));
        Assert.Equal(20, TestContexts.R5.Primitives.Definitions.Count);
        Assert.NotEqual(registryType.Assembly, typeof(PrimitiveRegistry).Assembly);
        Assert.Equal(
            TestContexts.R5.Primitives.Definitions.Count,
            TestContexts.R5.Primitives.Definitions
                .Select(definition => definition.FhirTypeName)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            TestContexts.R5.Primitives.Definitions.Count,
            TestContexts.R5.Primitives.Definitions
                .Select(definition => definition.PrimitiveType)
                .Distinct()
                .Count());
    }
}
