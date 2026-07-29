using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Parsing;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Parsing;

public sealed class StructureDefinitionElementSelectorTests
{
    private readonly StructureDefinitionElementSelector _selector = new();

    [Fact]
    public void Select_WithOfficialHumanNameFixture_SelectsDeclaredDirectChildren()
    {
        var definition = LoadHumanNameFixture();

        var result = _selector.Select(definition, "StructureDefinition-HumanName.json");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            [
                "HumanName.use",
                "HumanName.text",
                "HumanName.family",
                "HumanName.given",
                "HumanName.prefix",
                "HumanName.suffix",
                "HumanName.period"
            ],
            result.Value.Select(element => element.SnapshotElement.Id));
        Assert.Equal(
            Enumerable.Range(0, 7),
            result.Value.Select(element => element.Order));
        Assert.DoesNotContain(
            result.Value,
            element => element.SnapshotElement.Id == "HumanName.id");
        Assert.DoesNotContain(
            result.Value,
            element => element.SnapshotElement.Id == "HumanName.extension");

        var given = Assert.Single(
            result.Value,
            element => element.SnapshotElement.Id == "HumanName.given");
        Assert.Equal(0, given.SnapshotElement.Min);
        Assert.Equal("*", given.SnapshotElement.Max);
        Assert.Equal("string", Assert.Single(given.SnapshotElement.Types!).Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_WithMismatchedRoot_ReturnsInvalidInput(bool mismatchSnapshot)
    {
        var definition = CreateDefinition();
        var mismatchedRoot = CreateElement("Other", "Other");

        if (mismatchSnapshot)
        {
            definition = CopyWith(
                definition,
                snapshotElements:
                [
                    mismatchedRoot,
                    CreateElement("HumanName.family", "HumanName.family")
                ]);
        }
        else
        {
            definition = CopyWith(
                definition,
                differentialElements:
                [
                    mismatchedRoot,
                    CreateElement("HumanName.family", "HumanName.family")
                ]);
        }

        var result = _selector.Select(definition, "HumanName.json");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.InvalidInput &&
                diagnostic.Message.Contains("root element", StringComparison.Ordinal));
    }

    [Fact]
    public void Select_WhenSnapshotElementIsMissing_ReturnsInvalidInput()
    {
        var definition = CreateDefinition(
            snapshotElements: [CreateElement("HumanName", "HumanName")]);

        var result = _selector.Select(definition, "HumanName.json");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.InvalidInput, diagnostic.Code);
        Assert.Equal("HumanName.family", diagnostic.ElementId);
    }

    [Fact]
    public void Select_WithDeepChild_ReturnsUnsupportedDefinition()
    {
        var deepChild = CreateElement(
            "HumanName.family.extension",
            "HumanName.family.extension");
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                deepChild
            ],
            differentialElements:
            [
                CreateElement("HumanName", "HumanName"),
                deepChild
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.UnsupportedDefinition,
            "HumanName.family.extension");
    }

    [Theory]
    [InlineData("HumanName.id")]
    [InlineData("HumanName.extension")]
    public void Select_WithInheritedElementOverride_ReturnsUnsupportedDefinition(
        string elementId)
    {
        var inheritedElement = CreateElement(elementId, elementId);
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                inheritedElement
            ],
            differentialElements:
            [
                CreateElement("HumanName", "HumanName"),
                inheritedElement
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.UnsupportedDefinition,
            elementId);
    }

    [Fact]
    public void Select_WithSliceName_ReturnsFsg0006()
    {
        var slicedElement = CreateElement(
            "HumanName.family:official",
            "HumanName.family",
            sliceName: "official");
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                slicedElement
            ],
            differentialElements:
            [
                CreateElement("HumanName", "HumanName"),
                slicedElement
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.UnsupportedSlicing,
            "HumanName.family:official");
    }

    [Fact]
    public void Select_WithSlicingDefinition_ReturnsFsg0006()
    {
        var slicing = JsonDocument.Parse("{}").RootElement.Clone();
        var slicedElement = CreateElement(
            "HumanName.family",
            "HumanName.family",
            slicing: slicing);
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                slicedElement
            ],
            differentialElements:
            [
                CreateElement("HumanName", "HumanName"),
                slicedElement
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.UnsupportedSlicing,
            "HumanName.family");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_WithChoiceType_ReturnsFsg0007(bool useChoicePath)
    {
        var choiceElement = useChoicePath
            ? CreateElement("HumanName.value[x]", "HumanName.value[x]")
            : CreateElement(
                "HumanName.value",
                "HumanName.value",
                types:
                [
                    new ElementTypeDto { Code = "string" },
                    new ElementTypeDto { Code = "Period" }
                ]);
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                choiceElement
            ],
            differentialElements:
            [
                CreateElement("HumanName", "HumanName"),
                choiceElement
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.UnsupportedChoiceType,
            choiceElement.Id!);
    }

    [Fact]
    public void Select_WithContentReference_ReturnsFsg0008()
    {
        var referencedElement = CreateElement(
            "HumanName.family",
            "HumanName.family",
            contentReference: "#HumanName.text");
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                referencedElement
            ],
            differentialElements:
            [
                CreateElement("HumanName", "HumanName"),
                referencedElement
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.UnsupportedContentReference,
            "HumanName.family");
    }

    [Fact]
    public void Select_WithDuplicateSnapshotId_ReturnsInvalidInput()
    {
        var family = CreateElement("HumanName.family", "HumanName.family");
        var definition = CreateDefinition(
            snapshotElements:
            [
                CreateElement("HumanName", "HumanName"),
                family,
                family
            ]);

        var result = _selector.Select(definition, "HumanName.json");

        AssertUnsupported(
            result,
            GeneratorDiagnosticCodes.InvalidInput,
            "HumanName.family");
    }

    private static void AssertUnsupported(
        GenerationResult<IReadOnlyList<SelectedElementDefinition>> result,
        string expectedCode,
        string expectedElementId)
    {
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == expectedCode &&
                diagnostic.ElementId == expectedElementId);
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

        return JsonSerializer.Deserialize<StructureDefinitionDto>(json)
            ?? throw new InvalidOperationException(
                "The HumanName fixture could not be deserialized.");
    }

    private static StructureDefinitionDto CreateDefinition(
        List<ElementDefinitionDto>? snapshotElements = null,
        List<ElementDefinitionDto>? differentialElements = null)
    {
        var root = CreateElement("HumanName", "HumanName");
        var family = CreateElement(
            "HumanName.family",
            "HumanName.family",
            types: [new ElementTypeDto { Code = "string" }]);

        return new StructureDefinitionDto
        {
            ResourceType = "StructureDefinition",
            Id = "HumanName",
            Url = "http://hl7.org/fhir/StructureDefinition/HumanName",
            Version = "5.0.0",
            Name = "HumanName",
            Type = "HumanName",
            Kind = "complex-type",
            IsAbstract = false,
            BaseDefinition = "http://hl7.org/fhir/StructureDefinition/DataType",
            Derivation = "specialization",
            Snapshot = new StructureDefinitionSnapshotDto
            {
                Elements = snapshotElements ?? [root, family]
            },
            Differential = new StructureDefinitionDifferentialDto
            {
                Elements = differentialElements ?? [root, family]
            }
        };
    }

    private static StructureDefinitionDto CopyWith(
        StructureDefinitionDto definition,
        List<ElementDefinitionDto>? snapshotElements = null,
        List<ElementDefinitionDto>? differentialElements = null)
    {
        return new StructureDefinitionDto
        {
            ResourceType = definition.ResourceType,
            Id = definition.Id,
            Url = definition.Url,
            Version = definition.Version,
            Name = definition.Name,
            Type = definition.Type,
            Kind = definition.Kind,
            IsAbstract = definition.IsAbstract,
            BaseDefinition = definition.BaseDefinition,
            Derivation = definition.Derivation,
            Snapshot = new StructureDefinitionSnapshotDto
            {
                Elements = snapshotElements ?? definition.Snapshot?.Elements
            },
            Differential = new StructureDefinitionDifferentialDto
            {
                Elements = differentialElements ?? definition.Differential?.Elements
            }
        };
    }

    private static ElementDefinitionDto CreateElement(
        string id,
        string path,
        string? sliceName = null,
        JsonElement? slicing = null,
        string? contentReference = null,
        List<ElementTypeDto>? types = null)
    {
        return new ElementDefinitionDto
        {
            Id = id,
            Path = path,
            SliceName = sliceName,
            Slicing = slicing,
            Min = 0,
            Max = "1",
            ContentReference = contentReference,
            Types = types
        };
    }
}
