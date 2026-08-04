using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace MyFhirSdk.CodeGen.Tests.Parsing;

public sealed class StructureDefinitionParserWalkthroughTests
{
    private const string TargetNamespace =
        "MyFhirSdk.GeneratorFixtures.Types";

    private readonly ITestOutputHelper _output;

    public StructureDefinitionParserWalkthroughTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WalkThroughHumanNameParserPipeline()
    {
        var fixturePath = GetHumanNameFixturePath();
        var definition = LoadDefinition(fixturePath);

        PrintDefinitionMetadata(definition);

        var selector = new StructureDefinitionElementSelector();
        var selectionResult = selector.Select(definition, fixturePath);

        PrintSelectedElements(selectionResult);
        Assert.True(selectionResult.IsSuccess);

        PrintPropertyMappings(selectionResult.Value);

        var parser = new StructureDefinitionParser();
        IReadOnlySet<string> previewTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "HumanName"
            };

        var parseResult = parser.Parse(
            new LoadedStructureDefinition(fixturePath, definition),
            TargetNamespace,
            previewTypes);

        PrintFinalModel(parseResult);

        Assert.True(parseResult.IsSuccess);
        Assert.NotNull(parseResult.Value);
    }

    private void PrintDefinitionMetadata(StructureDefinitionDto definition)
    {
        _output.WriteLine("Step 1: StructureDefinition metadata");
        _output.WriteLine($"  Type: {definition.Type}");
        _output.WriteLine($"  URL: {definition.Url}");
        _output.WriteLine($"  Version: {definition.Version}");
        _output.WriteLine($"  BaseDefinition: {definition.BaseDefinition}");
        _output.WriteLine($"  Abstract: {definition.IsAbstract}");
    }

    private void PrintSelectedElements(
        GenerationResult<IReadOnlyList<SelectedElementDefinition>> result)
    {
        _output.WriteLine(string.Empty);
        _output.WriteLine("Step 2: Snapshot/differential element selection");

        foreach (var selected in result.Value)
        {
            var snapshot = selected.SnapshotElement;
            _output.WriteLine(
                $"  [{selected.Order}] " +
                $"Id={snapshot.Id}, " +
                $"Path={snapshot.Path}, " +
                $"Min={snapshot.Min}, " +
                $"Max={snapshot.Max}, " +
                $"Type={snapshot.Types?.SingleOrDefault()?.Code}");
        }

        PrintDiagnostics(result.Diagnostics);
    }

    private void PrintPropertyMappings(
        IReadOnlyList<SelectedElementDefinition> selectedElements)
    {
        var nameConverter = new CSharpNameConverter();
        var typeMapper = new CSharpTypeMapper();
        var cardinalityMapper = new CardinalityMapper();
        var existingNames = new HashSet<string>(StringComparer.Ordinal);

        _output.WriteLine(string.Empty);
        _output.WriteLine("Step 3: Property name, type, and cardinality mapping");

        foreach (var selected in selectedElements)
        {
            var element = selected.SnapshotElement;
            var nameResult = nameConverter.ConvertPropertyName(
                element.Path,
                existingNames);
            if (nameResult.IsSuccess)
            {
                existingNames.Add(nameResult.Name!);
            }

            var typeCode = element.Types?.SingleOrDefault()?.Code;
            var typeWasMapped = typeMapper.TryMap(
                typeCode,
                out var typeMapping);
            var cardinalityWasMapped = cardinalityMapper.TryMap(
                element.Min,
                element.Max,
                out var cardinality);
            var cSharpType = typeWasMapped && typeMapping is not null
                ? typeMapping.CSharpTypeName
                : "<failed>";
            var isCollection = cardinalityWasMapped && cardinality is not null
                ? cardinality.IsCollection.ToString()
                : "<failed>";
            var isRequired = cardinalityWasMapped && cardinality is not null
                ? cardinality.IsRequired.ToString()
                : "<failed>";

            _output.WriteLine($"  Element: {element.Id}");
            _output.WriteLine(
                $"    Property name: {nameResult.Name ?? "<failed>"}");
            _output.WriteLine($"    Name result: {nameResult.Failure}");
            _output.WriteLine($"    FHIR type: {typeCode ?? "<missing>"}");
            _output.WriteLine($"    C# type: {cSharpType}");
            _output.WriteLine(
                $"    Cardinality: {element.Min}..{element.Max}");
            _output.WriteLine($"    IsCollection: {isCollection}");
            _output.WriteLine($"    IsRequired: {isRequired}");
        }
    }

    private void PrintFinalModel(
        GenerationResult<MyFhirSdk.CodeGen.Models.FhirTypeModel?> result)
    {
        _output.WriteLine(string.Empty);
        _output.WriteLine("Step 4: Final FhirTypeModel");
        _output.WriteLine($"  IsSuccess: {result.IsSuccess}");

        PrintDiagnostics(result.Diagnostics);

        if (result.Value is null)
        {
            _output.WriteLine("  Model: <null>");
            return;
        }

        var model = result.Value;
        _output.WriteLine($"  FHIR name: {model.FhirName}");
        _output.WriteLine($"  C# name: {model.CSharpName}");
        _output.WriteLine($"  Namespace: {model.Namespace}");
        _output.WriteLine($"  Base type: {model.CSharpBaseType}");
        _output.WriteLine($"  Abstract: {model.IsAbstract}");
        _output.WriteLine($"  Canonical: {model.SourceCanonical}");
        _output.WriteLine($"  Version: {model.SourceVersion}");

        foreach (var property in model.Properties)
        {
            _output.WriteLine(
                $"  Property [{property.Order}]: " +
                $"{property.CSharpType} {property.CSharpName}, " +
                $"FHIR={property.FhirName}, " +
                $"Cardinality={property.Min}..{property.Max}");
        }
    }

    private void PrintDiagnostics(
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            _output.WriteLine(
                $"  Diagnostic: [{diagnostic.Code}] {diagnostic.Message}");
        }
    }

    private static string GetHumanNameFixturePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Valid",
            "StructureDefinition-HumanName.json");
    }

    private static StructureDefinitionDto LoadDefinition(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);
        return JsonSerializer.Deserialize<StructureDefinitionDto>(json)!;
    }
}
