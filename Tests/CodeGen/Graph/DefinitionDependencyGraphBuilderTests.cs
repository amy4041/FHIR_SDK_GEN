using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Graph;

public sealed class DefinitionDependencyGraphBuilderTests
{
    private const string RootCanonical = "http://example.test/StructureDefinition/Base";

    [Fact]
    public void Build_WithInheritanceCycle_ReportsDeterministicCycle()
    {
        var definitions = new[]
        {
            CreateDefinition("Base", null),
            CreateDefinition("A", Canonical("B")),
            CreateDefinition("B", Canonical("A"))
        };

        var result = BuildGraph(definitions);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.InheritanceCycle, diagnostic.Code);
        Assert.Contains(Canonical("A"), diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(Canonical("B"), diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithMissingTypeAndContentReference_ReportsBothEdges()
    {
        var element = CreateElement("A.child", "Missing");
        element = new ElementDefinitionDto
        {
            Id = element.Id,
            Path = element.Path,
            Base = element.Base,
            Types = element.Types,
            ContentReference = "#A.absent"
        };

        var result = BuildGraph([
            CreateDefinition("Base", null),
            CreateDefinition("A", RootCanonical, elements: [element])
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.MissingDependency));
        Assert.Equal(
            result.Diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.DefinitionCanonical, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.ElementId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal),
            result.Diagnostics);
    }

    [Fact]
    public void BuildAndSelect_WithMutualReferencesProfileAndContentReference_IsCycleSafe()
    {
        var aChild = CreateElement(
            "A.child",
            "B",
            targetProfiles: [Canonical("B")]);
        var aAlias = new ElementDefinitionDto
        {
            Id = "A.alias",
            Path = "A.alias",
            Base = new ElementDefinitionBaseDto { Path = "A.alias", Min = 0, Max = "1" },
            ContentReference = "#A.child"
        };
        var definitions = new[]
        {
            CreateDefinition("Base", null),
            CreateDefinition("A", RootCanonical, elements: [aChild, aAlias]),
            CreateDefinition("B", RootCanonical, elements: [CreateElement("B.parent", "A")])
        };

        var original = AssertGraph(BuildGraph(definitions));
        var reordered = AssertGraph(BuildGraph(definitions.Reverse()));
        Assert.Equal(Snapshot(original), Snapshot(reordered));
        Assert.Contains(original.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.TargetProfile);
        Assert.Contains(original.Edges, edge => edge.Kind == DefinitionDependencyEdgeKind.ContentReference);

        var scopeResult = new GenerationScopeSelector().Select(original, [Canonical("A")]);
        Assert.True(scopeResult.IsSuccess, Describe(scopeResult.Diagnostics));
        var scope = Assert.IsType<GenerationScope>(scopeResult.Value);
        Assert.Equal(
            new[] { "A", "B" },
            scope.GeneratedModels.Select(node => node.FhirTypeName).Order(StringComparer.Ordinal));
        Assert.Single(scope.ExternalDependencies, node => node.FhirTypeName == "Base");
    }

    [Fact]
    public void Build_WithIncompatibleResourceBase_ReportsKindMismatch()
    {
        var result = BuildGraph([
            CreateDefinition("Base", null),
            CreateDefinition("WrongResource", RootCanonical, kind: "resource")
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.IncompatibleInheritance);
    }

    private static GenerationResult<DefinitionDependencyGraph?> BuildGraph(
        IEnumerable<StructureDefinitionDto> definitions)
    {
        var package = new LoadedDefinitionPackage(
            new DefinitionPackageIdentity("example.test", "5.0.0", "Core", "5.0.0"),
            definitions.Select(definition => new LoadedStructureDefinition(
                $"package/{definition.Id}.json",
                definition)));
        var inventoryResult = new DefinitionInventoryBuilder().Build(package);
        Assert.True(inventoryResult.IsSuccess, Describe(inventoryResult.Diagnostics));
        return new DefinitionDependencyGraphBuilder().Build(
            Assert.IsType<DefinitionInventory>(inventoryResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            CreateOwnershipPolicy(),
            "ownership.json");
    }

    private static ModelOwnershipPolicyDocument CreateOwnershipPolicy() =>
        new()
        {
            SchemaVersion = 1,
            FhirVersion = "5.0.0",
            ExternalDefinitionNodes =
            [
                new ExternalDefinitionPolicyNode
                {
                    FhirType = "Base",
                    Canonical = RootCanonical,
                    Kind = "complex-type",
                    IsAbstract = true,
                    ClrType = "Example.Base",
                    GenerationDisposition = "external-handwritten"
                }
            ]
        };

    private static StructureDefinitionDto CreateDefinition(
        string type,
        string? baseCanonical,
        string kind = "complex-type",
        IEnumerable<ElementDefinitionDto>? elements = null)
    {
        var root = new ElementDefinitionDto
        {
            Id = type,
            Path = type,
            Min = 0,
            Max = "*"
        };
        var allElements = new[] { root }.Concat(elements ?? []).ToList();
        return new StructureDefinitionDto
        {
            ResourceType = "StructureDefinition",
            Id = type,
            Url = type == "Base" ? RootCanonical : Canonical(type),
            Version = "5.0.0",
            FhirVersion = "5.0.0",
            Name = type,
            Type = type,
            Kind = kind,
            IsAbstract = type == "Base",
            BaseDefinition = baseCanonical,
            Derivation = type == "Base" ? null : "specialization",
            Snapshot = new StructureDefinitionSnapshotDto { Elements = allElements },
            Differential = new StructureDefinitionDifferentialDto { Elements = [root] }
        };
    }

    private static ElementDefinitionDto CreateElement(
        string id,
        string typeCode,
        IEnumerable<string>? targetProfiles = null) =>
        new()
        {
            Id = id,
            Path = id,
            Base = new ElementDefinitionBaseDto { Path = id, Min = 0, Max = "1" },
            Min = 0,
            Max = "1",
            Types =
            [
                new ElementTypeDto
                {
                    Code = typeCode,
                    TargetProfiles = targetProfiles?.ToList()
                }
            ]
        };

    private static string Canonical(string type) =>
        $"http://example.test/StructureDefinition/{type}";

    private static DefinitionDependencyGraph AssertGraph(
        GenerationResult<DefinitionDependencyGraph?> result)
    {
        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        return Assert.IsType<DefinitionDependencyGraph>(result.Value);
    }

    private static string[] Snapshot(DefinitionDependencyGraph graph) =>
        graph.Nodes.Select(node => $"N|{node.Canonical}|{node.Disposition}")
            .Concat(graph.Edges.Select(edge => string.Join(
                '|',
                "E",
                edge.SourceCanonical,
                edge.SourceElementId,
                edge.Kind,
                edge.TargetCanonical,
                edge.TargetElementId,
                edge.ReferenceIdentity)))
            .ToArray();

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message));
}
