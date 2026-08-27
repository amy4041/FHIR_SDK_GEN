using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Parsing;

public sealed class StructureDefinitionParserTests
{
    private const string TargetNamespace =
        "MyFhirSdk.GeneratorFixtures.Types";

    private readonly StructureDefinitionParser _parser =
        PrimitivePolicyTestContext.CreateParser();

    [Fact]
    public void Parse_WithOfficialHumanNameFixture_ReturnsCompleteTypeModel()
    {
        var definition = LoadHumanNameFixture();

        var result = _parser.Parse(
            new LoadedStructureDefinition(
                "StructureDefinition-HumanName.json",
                definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal) { "HumanName" });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var model = Assert.IsType<FhirTypeModel>(result.Value);
        Assert.Equal("HumanName", model.FhirName);
        Assert.Equal("HumanName", model.CSharpName);
        Assert.Equal(TargetNamespace, model.Namespace);
        Assert.Equal("MyFhirSdk.Core.DataType", model.CSharpBaseType);
        Assert.False(model.IsAbstract);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/HumanName",
            model.SourceCanonical);
        Assert.Equal("5.0.0", model.SourceVersion);
        Assert.Equal(
            ["Use", "Text", "Family", "Given", "Prefix", "Suffix", "Period"],
            model.Properties.Select(property => property.CSharpName));
        Assert.Equal(
            Enumerable.Range(0, 7),
            model.Properties.Select(property => property.Order));

        var given = Assert.Single(
            model.Properties,
            property => property.ElementId == "HumanName.given");
        Assert.Equal("given", given.FhirName);
        Assert.Equal(
            "MyFhirSdk.Primitives.FhirString",
            given.CSharpType);
        Assert.Equal(0, given.Min);
        Assert.Equal("*", given.Max);
        Assert.True(given.IsCollection);
        Assert.False(given.IsRequired);

        var snapshotGiven = Assert.Single(
            definition.Snapshot!.Elements!,
            element => element.Id == "HumanName.given");
        Assert.Equal(snapshotGiven.Definition, given.Documentation);

        var period = Assert.Single(
            model.Properties,
            property => property.ElementId == "HumanName.period");
        Assert.Equal("MyFhirSdk.Types.Period", period.CSharpType);
    }

    [Fact]
    public void Parse_WithPreviewPropertyType_UsesTargetNamespace()
    {
        var definition = LoadHumanNameFixture();
        IReadOnlySet<string> previewTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "HumanName",
                "Period"
            };

        var result = _parser.Parse(
            new LoadedStructureDefinition("HumanName.json", definition),
            TargetNamespace,
            previewTypes);

        var model = Assert.IsType<FhirTypeModel>(result.Value);
        var period = Assert.Single(
            model.Properties,
            property => property.ElementId == "HumanName.period");
        Assert.Equal($"{TargetNamespace}.Period", period.CSharpType);
    }

    [Fact]
    public void Parse_UsesResolvedSnapshotPropertyValues()
    {
        var snapshotProperty = CreateElement(
            "Sample.value",
            min: 0,
            max: "*",
            typeCode: "string",
            definition: "Documentation resolved by snapshot.");
        var differentialProperty = CreateElement(
            "Sample.value",
            min: 1,
            max: "1",
            typeCode: "Period",
            definition: "Differential documentation.");
        var definition = CreateDefinition(
            snapshotElements: [CreateElement("Sample"), snapshotProperty],
            differentialElements: [CreateElement("Sample"), differentialProperty]);

        var result = _parser.Parse(
            new LoadedStructureDefinition("Sample.json", definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal));

        var model = Assert.IsType<FhirTypeModel>(result.Value);
        var property = Assert.Single(model.Properties);
        Assert.Equal("value", property.FhirName);
        Assert.Equal("Value", property.CSharpName);
        Assert.Equal(
            "MyFhirSdk.Primitives.FhirString",
            property.CSharpType);
        Assert.Equal(0, property.Min);
        Assert.Equal("*", property.Max);
        Assert.True(property.IsCollection);
        Assert.Equal(
            "Documentation resolved by snapshot.",
            property.Documentation);
    }

    [Fact]
    public void Parse_WithUnknownTypeAndUnsupportedCardinality_ReturnsAllDiagnostics()
    {
        var unknownType = CreateElement(
            "Sample.unknown",
            min: 0,
            max: "1",
            typeCode: "FutureType");
        var unsupportedCardinality = CreateElement(
            "Sample.values",
            min: 0,
            max: "2",
            typeCode: "string");
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("Sample"),
                unknownType,
                unsupportedCardinality
            ],
            differentialElements:
            [
                CreateElement("Sample"),
                CreateElement("Sample.unknown"),
                CreateElement("Sample.values")
            ]);

        var result = _parser.Parse(
            new LoadedStructureDefinition("Sample.json", definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.MissingTypeMapping &&
                diagnostic.ElementId == "Sample.unknown");
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedDefinition &&
                diagnostic.ElementId == "Sample.values");
        Assert.All(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal("Sample.json", diagnostic.SourceFile);
                Assert.Equal(definition.Url, diagnostic.DefinitionCanonical);
                Assert.Equal(definition.Version, diagnostic.DefinitionVersion);
            });
    }

    [Fact]
    public void Parse_WithConvertedPropertyNameConflict_ReturnsFsg0010()
    {
        var firstProperty = CreateElement(
            "Sample.family-name",
            min: 0,
            max: "1",
            typeCode: "string");
        var conflictingProperty = CreateElement(
            "Sample.family_name",
            min: 0,
            max: "1",
            typeCode: "string");
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("Sample"),
                firstProperty,
                conflictingProperty
            ],
            differentialElements:
            [
                CreateElement("Sample"),
                firstProperty,
                conflictingProperty
            ]);

        var result = _parser.Parse(
            new LoadedStructureDefinition("Sample.json", definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == GeneratorDiagnosticCodes.CSharpNameConflict);
        Assert.Equal("Sample.family_name", diagnostic.ElementId);
        Assert.Equal("Sample.family_name", diagnostic.ElementPath);
        Assert.Contains("FamilyName", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithUnsupportedBaseDefinition_ReturnsNoModel()
    {
        var definition = CreateDefinition(
            baseDefinition:
                "http://hl7.org/fhir/StructureDefinition/BackboneElement");

        var result = _parser.Parse(
            new LoadedStructureDefinition("Sample.json", definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.UnsupportedDefinition,
            diagnostic.Code);
        Assert.Contains("Base definition", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithMissingRequiredMetadata_ReturnsContextualDiagnostics()
    {
        var definition = CreateDefinition(
            type: null,
            url: null,
            version: null,
            baseDefinition: null,
            isAbstract: null);

        var result = _parser.Parse(
            new LoadedStructureDefinition("Invalid.json", definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(5, result.Diagnostics.Count);
        Assert.All(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(GeneratorDiagnosticCodes.InvalidInput, diagnostic.Code);
                Assert.Equal("Invalid.json", diagnostic.SourceFile);
            });
    }

    [Fact]
    public void Parse_WhenElementSelectionFails_PropagatesSelectorDiagnostic()
    {
        var slicedProperty = new ElementDefinitionDto
        {
            Id = "Sample.value:official",
            Path = "Sample.value",
            SliceName = "official",
            Min = 0,
            Max = "1",
            Types = [new ElementTypeDto { Code = "string" }]
        };
        var definition = CreateDefinition(
            snapshotElements: [CreateElement("Sample"), slicedProperty],
            differentialElements: [CreateElement("Sample"), slicedProperty]);

        var result = _parser.Parse(
            new LoadedStructureDefinition("Sample.json", definition),
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedSlicing &&
                diagnostic.ElementId == "Sample.value:official");
    }

    private static StructureDefinitionDto LoadHumanNameFixture()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Valid",
            "StructureDefinition-HumanName.json");
        var json = File.ReadAllText(fixturePath);

        return JsonSerializer.Deserialize<StructureDefinitionDto>(json)!;
    }

    private static StructureDefinitionDto CreateDefinition(
        IReadOnlyList<ElementDefinitionDto>? snapshotElements = null,
        IReadOnlyList<ElementDefinitionDto>? differentialElements = null,
        string? type = "Sample",
        string? url = "http://example.org/StructureDefinition/Sample",
        string? version = "5.0.0",
        string? baseDefinition =
            "http://hl7.org/fhir/StructureDefinition/DataType",
        bool? isAbstract = false)
    {
        snapshotElements ??= [CreateElement(type ?? "Sample")];
        differentialElements ??= [CreateElement(type ?? "Sample")];

        return new StructureDefinitionDto
        {
            ResourceType = "StructureDefinition",
            Id = type,
            Name = type,
            Type = type,
            Url = url,
            Version = version,
            Kind = "complex-type",
            IsAbstract = isAbstract,
            BaseDefinition = baseDefinition,
            Derivation = "specialization",
            Snapshot = new StructureDefinitionSnapshotDto
            {
                Elements = snapshotElements.ToList()
            },
            Differential = new StructureDefinitionDifferentialDto
            {
                Elements = differentialElements.ToList()
            }
        };
    }

    private static ElementDefinitionDto CreateElement(
        string idAndPath,
        int? min = null,
        string? max = null,
        string? typeCode = null,
        string? definition = null)
    {
        return new ElementDefinitionDto
        {
            Id = idAndPath,
            Path = idAndPath,
            Min = min,
            Max = max,
            Types = typeCode is null
                ? null
                : [new ElementTypeDto { Code = typeCode }],
            Definition = definition
        };
    }
}
