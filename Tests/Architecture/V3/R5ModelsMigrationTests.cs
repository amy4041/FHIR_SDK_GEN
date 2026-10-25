using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;

namespace MyFhirSdk.Tests.Architecture.V3;

public sealed class R5ModelsMigrationTests
{
    [Fact]
    public void R5ModelsAndSupportHaveNoSdkOrEngineReferenceOrFriendAccess()
    {
        var models = typeof(MyFhirSdk.Resources.Patient).Assembly;
        var sdk = typeof(FhirJsonParser).Assembly;
        Assert.Equal("MyFhirSdk.R5.Models", models.GetName().Name);
        Assert.Equal("MyFhirSdk", sdk.GetName().Name);
        Assert.NotSame(models, sdk);
        foreach (var assembly in new[] { models, typeof(MyFhirSdk.ModelSupport.ModelRules).Assembly })
        {
            Assert.All(assembly.GetReferencedAssemblies(), reference => Assert.True(reference.Name!.StartsWith("System.") ||
                reference.Name is "MyFhirSdk.Runtime" or "MyFhirSdk.ModelContracts" or "MyFhirSdk.ModelSupport", reference.FullName));
            Assert.DoesNotContain(assembly.GetCustomAttributesData(), a => a.AttributeType.Name == "InternalsVisibleToAttribute");
            Assert.Empty(assembly.GetForwardedTypes());
        }
        Assert.Empty(sdk.GetForwardedTypes());
        Assert.DoesNotContain(sdk.GetTypes(), t => t.Namespace is "MyFhirSdk.Resources" or "MyFhirSdk.Types");
        Assert.DoesNotContain(sdk.GetExportedTypes(), t => t.Namespace == "MyFhirSdk.Primitives");
        Assert.Contains(", MyFhirSdk.R5.Models,", typeof(MyFhirSdk.Resources.Patient).AssemblyQualifiedName);
        Assert.Same(models, typeof(MyFhirSdk.Types.SimpleQuantity).Assembly);
        Assert.Same(models, typeof(MyFhirSdk.Primitives.FhirString).Assembly);
    }

    [Fact]
    public void ProviderInventoryOwnsAllModelTypesAndDefaultContextUsesTheirFactories()
    {
        var models = typeof(R5ModelProvider).Assembly;
        var types = models.GetExportedTypes().Where(t => typeof(FhirObject).IsAssignableFrom(t)).ToHashSet();
        Assert.Equal(850, types.Count);
        Assert.True(types.SetEquals(R5ModelProvider.Metadata.ModelTypes));
        Assert.All(R5ModelProvider.Primitives.Definitions, p => Assert.Same(models, p.PrimitiveType.Assembly));
        Assert.Equal(R5ModelProvider.Identity, TestContexts.R5.Identity);
        foreach (var resource in R5ModelProvider.Metadata.Resources)
        {
            Assert.Equal(resource.ResourceType, resource.Factory().GetType());
            Assert.Equal(resource.ResourceType, TestContexts.R5.CreateResource(resource.FhirTypeName).GetType());
        }
        Assert.All(R5ModelProvider.Validation.Rules, rule => Assert.True(
            rule.AppliesTo.Assembly == models || rule.AppliesTo.Assembly == typeof(FhirObject).Assembly));
    }

    [Fact]
    public async Task EvaluatedSdkModelsAndSupportHaveDisjointSourcesAndOneGeneratedOwner()
    {
        var root = RepositoryRoot();
        var projects = new[] { "MyFhirSdk.csproj", "Models/R5/MyFhirSdk.R5.Models.csproj", "ModelSupport/MyFhirSdk.ModelSupport.csproj" };
        var evaluations = await Task.WhenAll(projects.Select(p => RuntimePhysicalOwnershipTests.Evaluate(Path.Combine(root, p))));
        try
        {
            var sets = evaluations.Select(e => e.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
                .Select(i => i.GetProperty("FullPath").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase)).ToArray();
            Assert.Empty(sets[0].Intersect(sets[1])); Assert.Empty(sets[0].Intersect(sets[2])); Assert.Empty(sets[1].Intersect(sets[2]));
            var generated = Directory.EnumerateFiles(Path.Combine(root, "Generated", "R5"), "*.cs", SearchOption.AllDirectories).ToArray();
            Assert.Equal(852, generated.Length);
            Assert.All(generated, file => { Assert.Contains(file, sets[1]); Assert.DoesNotContain(file, sets[0]); });
            Assert.All(sets[1], file => Assert.DoesNotContain(Path.DirectorySeparatorChar + "Serialization" + Path.DirectorySeparatorChar, file));
        }
        finally { foreach (var evaluation in evaluations) evaluation.Dispose(); }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "MyFhirSdk.sln"))) return dir.FullName;
        throw new InvalidOperationException("Requires source checkout.");
    }
}
