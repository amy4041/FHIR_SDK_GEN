using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Ir;

public sealed class ModelIrBuilderTests
{
    private const string RootCanonical = "http://example.test/StructureDefinition/Base";

    internal static async Task<ModelIrBatch> BuildChoiceDatatypeIrAsync()
    {
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Primitive("boolean"))
            .Append(Definition(
                "ChoicePayload",
                RootCanonical,
                elements:
                [
                    Element(
                        "ChoicePayload.value[x]",
                        "string",
                        "boolean",
                        min: 1,
                        max: "1")
                ]))
            .ToArray();
        var (graph, scope) = BuildGraphAndScope(definitions, Canonical("ChoicePayload"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);
        var result = new ModelIrBuilder().Build(
            graph,
            scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);
        Assert.True(result.IsSuccess, R5ModelIrTests.Describe(result.Diagnostics));
        return Assert.IsType<ModelIrBatch>(result.Value);
    }

    internal static async Task<ModelIrBatch> BuildRecursiveDatatypeIrAsync()
    {
        var alias = new ElementDefinitionDto
        {
            Id = "Recursive.alias",
            Path = "Recursive.alias",
            Base = Base("Recursive.alias", 0, "1"),
            Min = 0,
            Max = "1",
            ContentReference = "#Recursive.value"
        };
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Definition(
                "Recursive",
                RootCanonical,
                elements:
                [
                    Element("Recursive.value", "string"),
                    Element("Recursive.child", "Recursive"),
                    alias
                ]))
            .ToArray();
        var (graph, scope) = BuildGraphAndScope(definitions, Canonical("Recursive"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);
        var result = new ModelIrBuilder().Build(
            graph,
            scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);
        Assert.True(result.IsSuccess, R5ModelIrTests.Describe(result.Diagnostics));
        return Assert.IsType<ModelIrBatch>(result.Value);
    }

    [Fact]
    public async Task Build_WithChoiceOpenTypeBackboneAndContentReference_PreservesCompleteShape()
    {
        var parameter = Element(
            "Parameters.parameter",
            "BackboneElement",
            min: 1,
            max: "*");
        var openValue = Element(
            "Parameters.parameter.value[x]",
            "string",
            "Payload",
            min: 0,
            max: "1");
        var part = new ElementDefinitionDto
        {
            Id = "Parameters.parameter.part",
            Path = "Parameters.parameter.part",
            Base = Base("Parameters.parameter.part", 0, "*"),
            Min = 0,
            Max = "*",
            ContentReference = "#Parameters.parameter",
            Constraints =
            [
                new ElementConstraintDto
                {
                    Key = "par-1",
                    Severity = "error",
                    Human = "A parameter must have content.",
                    Expression = "name.exists()"
                }
            ],
            Binding = new ElementBindingDto
            {
                Strength = "example",
                ValueSet = "http://example.test/ValueSet/parameter"
            }
        };
        var ordinary = new ElementDefinitionDto
        {
            Id = "Parameters.choice[x]",
            Path = "Parameters.choice[x]",
            Base = Base("Parameters.choice[x]", 1, "1"),
            Min = 1,
            Max = "1",
            Types =
            [
                new ElementTypeDto
                {
                    Code = "string",
                    Profiles = [Canonical("Payload")]
                },
                new ElementTypeDto
                {
                    Code = "Payload",
                    TargetProfiles = [Canonical("Payload")]
                }
            ]
        };
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Definition("Payload", RootCanonical))
            .Append(Definition(
                "Parameters",
                Canonical("Resource"),
                "resource",
                [parameter, openValue, part, ordinary]))
            .ToArray();

        var (graph, scope) = BuildGraphAndScope(definitions, Canonical("Parameters"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);
        var result = new ModelIrBuilder().Build(
            graph,
            scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.True(result.IsSuccess, R5ModelIrTests.Describe(result.Diagnostics));
        var batch = Assert.IsType<ModelIrBatch>(result.Value);
        var resource = Assert.Single(batch.Declarations, declaration =>
            declaration.FhirName == "Parameters");
        var backbone = Assert.Single(batch.Declarations, declaration =>
            declaration.Category == ModelIrCategory.Backbone);
        Assert.Equal("ParametersParameter", backbone.CSharpName);
        Assert.True(backbone.IsSealed);
        Assert.Equal(Canonical("Parameters"), backbone.ResourceOwnerCanonical);
        Assert.Equal("Parameters.parameter", backbone.BackboneElementId);
        Assert.Equal("MyFhirSdk.Core.BackboneElement", backbone.BaseType.ClrType);
        Assert.Equal(
            "MyFhirSdk.Resources.ParametersParameter",
            Assert.Single(resource.Members, member =>
                member.Source.ElementId == "Parameters.parameter").Properties.Single().CSharpType);

        var open = Assert.Single(backbone.Members, member =>
            member.Source.ElementId == "Parameters.parameter.value[x]");
        Assert.Equal(ModelMemberRepresentation.OpenType, open.Representation);
        Assert.Equal("value[x]", open.FhirName);
        Assert.Equal("value", open.ChoiceStem);
        Assert.Equal(2, open.TypeAlternatives.Count);
        var openProperty = Assert.Single(open.Properties);
        Assert.Equal("Value", openProperty.CSharpName);
        Assert.Equal("MyFhirSdk.Core.DataType", openProperty.CSharpType);
        Assert.True(openProperty.IsNullable);

        var content = Assert.Single(backbone.Members, member =>
            member.Source.ElementId == "Parameters.parameter.part");
        Assert.Equal(ModelMemberRepresentation.ContentReference, content.Representation);
        Assert.Equal("Parameters.parameter", content.ResolvedContentTarget?.ElementId);
        Assert.True(Assert.Single(content.Properties).IsCollection);
        Assert.Equal("par-1", Assert.Single(content.Validation.Constraints).Key);
        Assert.Equal("example", content.Validation.Binding?.Strength);

        var choice = Assert.Single(resource.Members, member =>
            member.Source.ElementId == "Parameters.choice[x]");
        Assert.Equal(ModelMemberRepresentation.OrdinaryChoice, choice.Representation);
        Assert.True(choice.Cardinality.IsRequired);
        Assert.Equal(
            new[] { "ChoicePayload", "ChoiceString" },
            choice.Properties.Select(property => property.CSharpName).Order());
        Assert.All(choice.Properties, property => Assert.True(property.IsNullable));
        Assert.Contains(choice.TypeAlternatives, alternative =>
            alternative.FhirTypeCode == "Payload" &&
            alternative.ClrType == "MyFhirSdk.Types.Payload" &&
            alternative.TargetProfiles.SequenceEqual([Canonical("Payload")]));
        Assert.Contains(choice.TypeAlternatives, alternative =>
            alternative.FhirTypeCode == "string" &&
            alternative.Profiles.SequenceEqual([Canonical("Payload")]));
    }

    [Fact]
    public async Task Build_WithMemberCollision_FailsWithoutPartialBatch()
    {
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Definition(
                "Parameters",
                Canonical("Resource"),
                "resource",
                [Element("Parameters.resourceType", "string")]))
            .ToArray();
        var (graph, scope) = BuildGraphAndScope(definitions, Canonical("Parameters"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);

        var result = new ModelIrBuilder().Build(
            graph,
            scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.ModelIrCollision &&
            diagnostic.ElementId == "Parameters.resourceType");
    }

    [Fact]
    public async Task Build_WithUnsupportedCardinality_FailsWithoutPartialBatch()
    {
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Definition(
                "Payload",
                RootCanonical,
                elements: [Element("Payload.value", "string", min: 2, max: "2")]))
            .ToArray();
        var (graph, scope) = BuildGraphAndScope(definitions, Canonical("Payload"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);

        var result = new ModelIrBuilder().Build(
            graph,
            scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedModelShape);
    }

    [Fact]
    public async Task Build_WithInheritedMemberCollision_FailsWithoutPartialBatch()
    {
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Definition(
                "Parent",
                RootCanonical,
                elements: [Element("Parent.value", "string")]))
            .Append(Definition(
                "Child",
                Canonical("Parent"),
                elements: [Element("Child.value", "string")]))
            .ToArray();
        var (graph, scope) = BuildGraphAndScope(definitions, Canonical("Child"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);

        var result = new ModelIrBuilder().Build(
            graph,
            scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.ModelIrCollision &&
            diagnostic.ElementId == "Child.value");
    }

    [Fact]
    public async Task Build_WithReorderedDefinitions_ProducesIdenticalIr()
    {
        var definitions = FoundationDefinitions()
            .Append(Primitive("string"))
            .Append(Definition(
                "Payload",
                RootCanonical,
                elements: [Element("Payload.value", "string")]))
            .ToArray();
        var originalContext = BuildGraphAndScope(definitions, Canonical("Payload"));
        var reorderedContext = BuildGraphAndScope(definitions.Reverse(), Canonical("Payload"));
        var policy = Assert.IsType<ModelIrGenerationPolicy>((await R5ModelIrTests.LoadPolicy()).Value);
        var builder = new ModelIrBuilder();

        var original = builder.Build(
            originalContext.Graph,
            originalContext.Scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);
        var reordered = builder.Build(
            reorderedContext.Graph,
            reorderedContext.Scope,
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.True(original.IsSuccess, R5ModelIrTests.Describe(original.Diagnostics));
        Assert.True(reordered.IsSuccess, R5ModelIrTests.Describe(reordered.Diagnostics));
        Assert.Equal(
            Snapshot(Assert.IsType<ModelIrBatch>(original.Value)),
            Snapshot(Assert.IsType<ModelIrBatch>(reordered.Value)));
    }

    private static (DefinitionDependencyGraph Graph, GenerationScope Scope)
        BuildGraphAndScope(
            IEnumerable<StructureDefinitionDto> definitions,
            string seed)
    {
        var package = new LoadedDefinitionPackage(
            new DefinitionPackageIdentity("example.test", "5.0.0", "Core", "5.0.0"),
            definitions.Select(definition => new LoadedStructureDefinition(
                $"package/{definition.Id}.json",
                definition)));
        var inventoryResult = new DefinitionInventoryBuilder().Build(package);
        Assert.True(inventoryResult.IsSuccess, R5ModelIrTests.Describe(inventoryResult.Diagnostics));
        var graphResult = new DefinitionDependencyGraphBuilder().Build(
            Assert.IsType<DefinitionInventory>(inventoryResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            OwnershipPolicy(),
            "ownership.json");
        Assert.True(graphResult.IsSuccess, R5ModelIrTests.Describe(graphResult.Diagnostics));
        var graph = Assert.IsType<DefinitionDependencyGraph>(graphResult.Value);
        var scopeResult = new GenerationScopeSelector().Select(graph, [seed]);
        Assert.True(scopeResult.IsSuccess, R5ModelIrTests.Describe(scopeResult.Diagnostics));
        return (graph, Assert.IsType<GenerationScope>(scopeResult.Value));
    }

    private static IEnumerable<StructureDefinitionDto> FoundationDefinitions()
    {
        yield return Definition("Base", null, isAbstract: true);
        yield return Definition("BackboneElement", RootCanonical, isAbstract: true);
        yield return Definition("Resource", RootCanonical, "resource", isAbstract: true);
    }

    private static ModelOwnershipPolicyDocument OwnershipPolicy() =>
        new()
        {
            SchemaVersion = 1,
            FhirVersion = "5.0.0",
            ExternalDefinitionNodes =
            [
                External("Base", RootCanonical, "complex-type", null, "MyFhirSdk.Core.Base"),
                External("BackboneElement", Canonical("BackboneElement"), "complex-type", RootCanonical, "MyFhirSdk.Core.BackboneElement"),
                External("Resource", Canonical("Resource"), "resource", RootCanonical, "MyFhirSdk.Core.Resource")
            ]
        };

    private static ExternalDefinitionPolicyNode External(
        string type,
        string canonical,
        string kind,
        string? baseCanonical,
        string clrType) =>
        new()
        {
            FhirType = type,
            Canonical = canonical,
            Kind = kind,
            IsAbstract = true,
            BaseCanonical = baseCanonical,
            ClrType = clrType,
            GenerationDisposition = "external-handwritten"
        };

    private static StructureDefinitionDto Primitive(string type) =>
        Definition(
            type,
            Canonical("PrimitiveType"),
            "primitive-type");

    private static StructureDefinitionDto Definition(
        string type,
        string? baseCanonical,
        string kind = "complex-type",
        IEnumerable<ElementDefinitionDto>? elements = null,
        bool isAbstract = false)
    {
        var root = new ElementDefinitionDto
        {
            Id = type,
            Path = type,
            Min = 0,
            Max = "*"
        };
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
            IsAbstract = isAbstract,
            BaseDefinition = baseCanonical,
            Derivation = type == "Base" ? null : "specialization",
            Snapshot = new StructureDefinitionSnapshotDto
            {
                Elements = new[] { root }.Concat(elements ?? []).ToList()
            },
            Differential = new StructureDefinitionDifferentialDto { Elements = [root] }
        };
    }

    private static ElementDefinitionDto Element(
        string id,
        params string[] typeCodes) =>
        Element(id, typeCodes, 0, "1");

    private static ElementDefinitionDto Element(
        string id,
        string typeCode1,
        string typeCode2,
        int min,
        string max) =>
        Element(id, new[] { typeCode1, typeCode2 }, min, max);

    private static ElementDefinitionDto Element(
        string id,
        string typeCode,
        int min,
        string max) =>
        Element(id, new[] { typeCode }, min, max);

    private static ElementDefinitionDto Element(
        string id,
        IEnumerable<string> typeCodes,
        int min,
        string max) =>
        new()
        {
            Id = id,
            Path = id,
            Base = Base(id, min, max),
            Min = min,
            Max = max,
            Types = typeCodes.Select(code => new ElementTypeDto { Code = code }).ToList()
        };

    private static ElementDefinitionBaseDto Base(string path, int min, string max) =>
        new() { Path = path, Min = min, Max = max };

    private static string Canonical(string type) =>
        $"http://example.test/StructureDefinition/{type}";

    private static string[] Snapshot(ModelIrBatch batch) =>
        batch.Declarations.SelectMany(declaration =>
            new[]
            {
                $"D|{declaration.FullyQualifiedName}|{declaration.BaseType.ClrType}|{declaration.ArtifactPath}"
            }.Concat(declaration.Members.SelectMany(member =>
                new[]
                {
                    $"M|{member.Source.DefinitionCanonical}|{member.Source.ElementId}|{member.Representation}|{member.Cardinality.Min}..{member.Cardinality.Max}"
                }.Concat(member.Properties.Select(property =>
                    $"P|{property.JsonName}|{property.CSharpName}|{property.CSharpType}")))))
            .ToArray();
}
