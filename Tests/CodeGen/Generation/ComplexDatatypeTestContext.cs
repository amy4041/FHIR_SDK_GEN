using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

internal static class ComplexDatatypeTestContext
{
    internal static async Task<(DefinitionDependencyGraph Graph, ModelIrBatch Ir)>
        BuildOfficialIrAsync(params string[] typeNames)
    {
        var graph = await BuildOfficialGraphAsync();
        var canonicals = typeNames
            .Select(typeName => $"http://hl7.org/fhir/StructureDefinition/{typeName}")
            .ToArray();
        var scopeResult = new GenerationScopeSelector().Select(graph, canonicals);
        Assert.True(scopeResult.IsSuccess, Describe(scopeResult.Diagnostics));
        var policyResult = await new ModelIrGenerationPolicyLoader().LoadAsync(
            new ModelIrPolicyPaths(
                PolicyPath("r5-model-naming-policy.json"),
                PolicyPath("r5-backbone-policy.json"),
                PolicyPath("r5-choice-open-type-policy.json")));
        Assert.True(policyResult.IsSuccess, Describe(policyResult.Diagnostics));
        var irResult = new ModelIrBuilder().Build(
            graph,
            Assert.IsType<GenerationScope>(scopeResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            Assert.IsType<ModelIrGenerationPolicy>(policyResult.Value));
        Assert.True(irResult.IsSuccess, Describe(irResult.Diagnostics));
        return (graph, Assert.IsType<ModelIrBatch>(irResult.Value));
    }

    internal static async Task<DefinitionDependencyGraph> BuildOfficialGraphAsync()
    {
        var inventoryResult = await new DefinitionInventoryPipeline().BuildAsync(
            new FileDefinitionPackageInput(GetOfficialPackagePath()),
            new DefinitionPackageLoadOptions("hl7.fhir.r5.core", "5.0.0", "5.0.0"));
        Assert.True(inventoryResult.IsSuccess, Describe(inventoryResult.Diagnostics));
        var ownershipPath = PolicyPath("r5-model-ownership-policy.json");
        var ownershipResult = await new ModelOwnershipPolicyLoader().LoadAsync(ownershipPath);
        Assert.True(ownershipResult.IsSuccess, Describe(ownershipResult.Diagnostics));
        var graphResult = new DefinitionDependencyGraphBuilder().Build(
            Assert.IsType<DefinitionInventory>(inventoryResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            Assert.IsType<ModelOwnershipPolicyDocument>(ownershipResult.Value),
            ownershipPath);
        Assert.True(graphResult.IsSuccess, Describe(graphResult.Diagnostics));
        return Assert.IsType<DefinitionDependencyGraph>(graphResult.Value);
    }

    internal static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.Code} {diagnostic.DefinitionCanonical} {diagnostic.ElementId}: {diagnostic.Message}"));

    private static string PolicyPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Policy", fileName);

    private static string GetOfficialPackagePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");
}
