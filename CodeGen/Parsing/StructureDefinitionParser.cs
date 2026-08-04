using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Parsing;

public sealed class StructureDefinitionParser
{
    private const string FhirDataTypeCanonical =
        "http://hl7.org/fhir/StructureDefinition/DataType";
    private const string CSharpDataTypeName = "MyFhirSdk.Core.DataType";

    private readonly StructureDefinitionElementSelector _elementSelector;
    private readonly CSharpTypeMapper _typeMapper;
    private readonly CSharpNameConverter _nameConverter;
    private readonly CardinalityMapper _cardinalityMapper;

    public StructureDefinitionParser(
        StructureDefinitionElementSelector? elementSelector = null,
        CSharpTypeMapper? typeMapper = null,
        CSharpNameConverter? nameConverter = null,
        CardinalityMapper? cardinalityMapper = null)
    {
        _elementSelector =
            elementSelector ?? new StructureDefinitionElementSelector();
        _typeMapper = typeMapper ?? new CSharpTypeMapper();
        _nameConverter = nameConverter ?? new CSharpNameConverter();
        _cardinalityMapper = cardinalityMapper ?? new CardinalityMapper();
    }

    public GenerationResult<FhirTypeModel?> Parse(
        LoadedStructureDefinition loadedDefinition,
        string targetNamespace,
        IReadOnlySet<string> previewFhirTypeNames)
    {
        ArgumentNullException.ThrowIfNull(loadedDefinition);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNamespace);
        ArgumentNullException.ThrowIfNull(previewFhirTypeNames);

        var definition = loadedDefinition.Definition;
        ArgumentNullException.ThrowIfNull(definition);

        var diagnostics = new List<GeneratorDiagnostic>();
        ValidateRequiredMetadata(
            definition,
            loadedDefinition.SourceFile,
            diagnostics);

        CSharpNameConversionResult? typeNameResult = null;
        if (!string.IsNullOrWhiteSpace(definition.Type))
        {
            typeNameResult = _nameConverter.ConvertTypeName(definition.Type);
            if (!typeNameResult.IsSuccess)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    definition,
                    loadedDefinition.SourceFile,
                    $"FHIR type name '{definition.Type}' cannot be converted to a " +
                    "valid C# type name."));
            }
        }

        ValidateBaseDefinition(
            definition,
            loadedDefinition.SourceFile,
            diagnostics);

        IReadOnlyList<SelectedElementDefinition> selectedElements = [];
        if (!string.IsNullOrWhiteSpace(definition.Type))
        {
            var selectionResult = _elementSelector.Select(
                definition,
                loadedDefinition.SourceFile);
            diagnostics.AddRange(selectionResult.Diagnostics);
            selectedElements = selectionResult.Value;
        }

        var properties = ParseProperties(
            selectedElements,
            definition,
            loadedDefinition.SourceFile,
            previewFhirTypeNames,
            targetNamespace,
            diagnostics);

        if (HasErrors(diagnostics))
        {
            return CreateResult(null, diagnostics);
        }

        var model = new FhirTypeModel(
            definition.Type!,
            typeNameResult!.Name!,
            targetNamespace,
            CSharpDataTypeName,
            definition.IsAbstract!.Value,
            definition.Url!,
            definition.Version!,
            properties);

        return CreateResult(model, diagnostics);
    }

    private IReadOnlyList<FhirPropertyModel> ParseProperties(
        IEnumerable<SelectedElementDefinition> selectedElements,
        StructureDefinitionDto definition,
        string sourceFile,
        IReadOnlySet<string> previewFhirTypeNames,
        string targetNamespace,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var properties = new List<FhirPropertyModel>();
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var selectedElement in selectedElements.OrderBy(
                     element => element.Order))
        {
            var element = selectedElement.SnapshotElement;
            var propertyHasErrors = false;
            var fhirName = GetLastPathSegment(element.Path!);

            var propertyNameResult = _nameConverter.ConvertPropertyName(
                element.Path,
                propertyNames);
            if (!propertyNameResult.IsSuccess)
            {
                var code = propertyNameResult.Failure ==
                    CSharpNameConversionFailure.Conflict
                        ? GeneratorDiagnosticCodes.CSharpNameConflict
                        : GeneratorDiagnosticCodes.InvalidInput;
                var message = propertyNameResult.Failure ==
                    CSharpNameConversionFailure.Conflict
                        ? $"C# property name '{propertyNameResult.Name}' conflicts with " +
                          "another property in the same type."
                        : $"FHIR element path '{element.Path}' cannot be converted to a " +
                          "valid C# property name.";
                diagnostics.Add(CreateDiagnostic(
                    code,
                    definition,
                    sourceFile,
                    message,
                    element));
                propertyHasErrors = true;
            }
            else
            {
                propertyNames.Add(propertyNameResult.Name!);
            }

            CSharpTypeMapping? typeMapping = null;
            if (element.Types is not { Count: 1 })
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    definition,
                    sourceFile,
                    $"Element '{element.Id}' must contain exactly one type.code.",
                    element));
                propertyHasErrors = true;
            }
            else if (string.IsNullOrWhiteSpace(element.Types[0].Code))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    definition,
                    sourceFile,
                    $"Element '{element.Id}' has an empty type.code.",
                    element));
                propertyHasErrors = true;
            }
            else if (!_typeMapper.TryMap(
                         element.Types[0].Code,
                         previewFhirTypeNames,
                         targetNamespace,
                         out typeMapping))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.MissingTypeMapping,
                    definition,
                    sourceFile,
                    $"No C# type mapping exists for FHIR type " +
                    $"'{element.Types[0].Code}'.",
                    element));
                propertyHasErrors = true;
            }

            if (!_cardinalityMapper.TryMap(
                    element.Min,
                    element.Max,
                    out var cardinality))
            {
                var code = element.Min is null ||
                           string.IsNullOrWhiteSpace(element.Max)
                    ? GeneratorDiagnosticCodes.InvalidInput
                    : GeneratorDiagnosticCodes.UnsupportedDefinition;
                diagnostics.Add(CreateDiagnostic(
                    code,
                    definition,
                    sourceFile,
                    $"Element '{element.Id}' has missing or unsupported cardinality " +
                    $"'{FormatCardinality(element.Min, element.Max)}'.",
                    element));
                propertyHasErrors = true;
            }

            if (propertyHasErrors)
            {
                continue;
            }

            properties.Add(new FhirPropertyModel(
                element.Id!,
                element.Path!,
                fhirName,
                propertyNameResult.Name!,
                typeMapping!.CSharpTypeName,
                cardinality!,
                GetDocumentation(element),
                selectedElement.Order));
        }

        return properties;
    }

    private static void ValidateRequiredMetadata(
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        RequireText(definition.Type, "type", definition, sourceFile, diagnostics);
        RequireText(definition.Url, "url", definition, sourceFile, diagnostics);
        RequireText(
            definition.Version,
            "version",
            definition,
            sourceFile,
            diagnostics);
        RequireText(
            definition.BaseDefinition,
            "baseDefinition",
            definition,
            sourceFile,
            diagnostics);

        if (definition.IsAbstract is null)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                definition,
                sourceFile,
                "The required field 'abstract' is missing."));
        }
    }

    private static void ValidateBaseDefinition(
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(definition.BaseDefinition) ||
            string.Equals(
                definition.BaseDefinition,
                FhirDataTypeCanonical,
                StringComparison.Ordinal))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.UnsupportedDefinition,
            definition,
            sourceFile,
            $"Base definition '{definition.BaseDefinition}' is not supported by the MVP."));
    }

    private static void RequireText(
        string? value,
        string fieldName,
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidInput,
            definition,
            sourceFile,
            $"The required field '{fieldName}' is missing."));
    }

    private static string GetLastPathSegment(string elementPath)
    {
        var separatorIndex = elementPath.LastIndexOf('.');
        return separatorIndex < 0
            ? elementPath
            : elementPath[(separatorIndex + 1)..];
    }

    private static string? GetDocumentation(ElementDefinitionDto element)
    {
        if (!string.IsNullOrWhiteSpace(element.Definition))
        {
            return element.Definition;
        }

        return string.IsNullOrWhiteSpace(element.Short)
            ? null
            : element.Short;
    }

    private static string FormatCardinality(int? min, string? max)
    {
        return $"{min?.ToString() ?? "<missing>"}..{max ?? "<missing>"}";
    }

    private static bool HasErrors(IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        return diagnostics.Any(diagnostic =>
            diagnostic.Severity == GeneratorDiagnosticSeverity.Error);
    }

    private static GeneratorDiagnostic CreateDiagnostic(
        string code,
        StructureDefinitionDto definition,
        string sourceFile,
        string message,
        ElementDefinitionDto? element = null)
    {
        return new GeneratorDiagnostic(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceFile,
            definition.Url,
            definition.Version,
            element?.Id,
            element?.Path);
    }

    private static GenerationResult<FhirTypeModel?> CreateResult(
        FhirTypeModel? model,
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<FhirTypeModel?>(
            model,
            diagnostics.ToArray());
    }
}
