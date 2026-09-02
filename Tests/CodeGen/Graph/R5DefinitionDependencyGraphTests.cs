using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Graph;

public sealed class R5DefinitionDependencyGraphTests
{
    [Fact]
    public async Task Build_WithOfficialR5Inventory_ProducesCompleteDeterministicGraph()
    {
        var graph = await BuildOfficialGraph();

        Assert.Equal(307, graph.Nodes.Count);
        AssertDispositionCount(graph, DefinitionDependencyNodeDisposition.GeneratedModel, 199);
        AssertDispositionCount(graph, DefinitionDependencyNodeDisposition.ExternalHandwritten, 11);
        AssertDispositionCount(graph, DefinitionDependencyNodeDisposition.SupportedPrimitive, 17);
        AssertDispositionCount(graph, DefinitionDependencyNodeDisposition.UnsupportedPrimitive, 4);
        AssertDispositionCount(graph, DefinitionDependencyNodeDisposition.ConstraintProfile, 66);
        AssertDispositionCount(graph, DefinitionDependencyNodeDisposition.LogicalModel, 10);
        Assert.All(graph.Edges, edge => Assert.True(graph.TryGetNode(edge.TargetCanonical, out _)));
        Assert.Contains(graph.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.Inheritance);
        Assert.Contains(graph.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.ElementType);
        Assert.Contains(graph.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.Profile);
        Assert.Contains(graph.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.TargetProfile);
        Assert.Contains(graph.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.ContentReference);
        Assert.Contains(graph.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.BackboneOwner);
        AssertEdgeCount(graph, DefinitionDependencyEdgeKind.Inheritance, 209);
        AssertEdgeCount(graph, DefinitionDependencyEdgeKind.ElementType, 6926);
        AssertEdgeCount(graph, DefinitionDependencyEdgeKind.Profile, 63);
        AssertEdgeCount(graph, DefinitionDependencyEdgeKind.TargetProfile, 2410);
        AssertEdgeCount(graph, DefinitionDependencyEdgeKind.ContentReference, 78);
        AssertEdgeCount(graph, DefinitionDependencyEdgeKind.BackboneOwner, 613);
        Assert.Equal(Snapshot(graph.Edges), Snapshot(graph.Edges.OrderBy(
            edge => edge.SourceCanonical,
            StringComparer.Ordinal)
            .ThenBy(edge => edge.SourceElementId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Kind)
            .ThenBy(edge => edge.TargetCanonical, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetElementId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ReferenceIdentity, StringComparer.Ordinal)));

        var mappingView = DefinitionTypeMappingView.FromGraph(graph);
        AssertMapping(mappingView, "Patient", "Patient", "MyFhirSdk.Resources");
        AssertMapping(mappingView, "Period", "Period", "MyFhirSdk.Types");
        AssertMapping(mappingView, "DomainResource", "DomainResource", "MyFhirSdk.Core");
    }

    [Fact]
    public async Task Select_WithPeriodCanonical_ProducesCycleSafeClosureAndPlan()
    {
        var graph = await BuildOfficialGraph();
        var result = new GenerationScopeSelector().Select(
            graph,
            ["http://hl7.org/fhir/StructureDefinition/Period"]);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var scope = Assert.IsType<GenerationScope>(result.Value);
        Assert.Contains(scope.GeneratedModels, node => node.FhirTypeName == "Period");
        Assert.Contains(scope.ExternalDependencies, node => node.FhirTypeName == "DataType");
        Assert.Equal(
            scope.GeneratedModels.OrderBy(node => node.Canonical, StringComparer.Ordinal),
            scope.GeneratedModels);
        Assert.Equal(
            Enumerable.Range(0, scope.GenerationPlan.Count),
            scope.GenerationPlan.Select(item => item.Ordinal));
    }

    [Fact]
    public async Task SelectAll_WhenUnsupportedPrimitivesAreReachable_ReportsDirectDiagnostics()
    {
        var graph = await BuildOfficialGraph();
        var result = new GenerationScopeSelector().SelectAll(graph);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedPrimitiveReference);
        Assert.Equal(41, result.Diagnostics.Count);
        Assert.Equal(
            new[] { "oid", "time", "uuid" },
            result.Diagnostics
                .Select(diagnostic => diagnostic.Message.Split('\'')[3])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        Assert.All(
            result.Diagnostics.Where(diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedPrimitiveReference),
            diagnostic => Assert.NotNull(diagnostic.ElementId));
    }

    private static async Task<DefinitionDependencyGraph> BuildOfficialGraph()
    {
        var inventoryResult = await new DefinitionInventoryPipeline().BuildAsync(
            new FileDefinitionPackageInput(GetOfficialPackagePath()),
            new DefinitionPackageLoadOptions(
                "hl7.fhir.r5.core",
                "5.0.0",
                "5.0.0"));
        Assert.True(inventoryResult.IsSuccess, Describe(inventoryResult.Diagnostics));

        var ownershipPath = Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "r5-model-ownership-policy.json");
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

    private static void AssertDispositionCount(
        DefinitionDependencyGraph graph,
        DefinitionDependencyNodeDisposition disposition,
        int expected) =>
        Assert.Equal(expected, graph.Nodes.Count(node => node.Disposition == disposition));

    private static void AssertEdgeCount(
        DefinitionDependencyGraph graph,
        DefinitionDependencyEdgeKind kind,
        int expected) =>
        Assert.Equal(expected, graph.Edges.Count(edge => edge.Kind == kind));

    private static void AssertMapping(
        DefinitionTypeMappingView view,
        string fhirTypeName,
        string expectedTypeName,
        string expectedNamespace)
    {
        Assert.True(view.TryGet(fhirTypeName, out var mapping));
        Assert.Equal(expectedTypeName, mapping.TypeName);
        Assert.Equal(expectedNamespace, mapping.Namespace);
    }

    private static string[] Snapshot(IEnumerable<DefinitionDependencyEdge> edges) =>
        edges.Select(edge => string.Join(
            '|',
            edge.SourceCanonical,
            edge.SourceElementId,
            edge.Kind,
            edge.TargetCanonical,
            edge.TargetElementId,
            edge.ReferenceIdentity)).ToArray();

    private static string GetOfficialPackagePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.Code} {diagnostic.DefinitionCanonical} {diagnostic.ElementId}: {diagnostic.Message}"));
}
