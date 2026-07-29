using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Parsing;

public sealed class StructureDefinitionElementSelector
{
    private static readonly HashSet<string> MvpInheritedElementNames =
        new(StringComparer.Ordinal)
        {
            "id",
            "extension"
        };

    public GenerationResult<IReadOnlyList<SelectedElementDefinition>> Select(
        StructureDefinitionDto definition,
        string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var selectedElements = new List<SelectedElementDefinition>();
        var diagnostics = new List<GeneratorDiagnostic>();

        if (string.IsNullOrWhiteSpace(definition.Type))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                definition,
                sourceFile,
                "The StructureDefinition type is required for element selection."));
            return CreateResult([], diagnostics);
        }

        var snapshotElements = definition.Snapshot?.Elements;
        if (snapshotElements is null or { Count: 0 })
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.MissingSnapshot,
                definition,
                sourceFile,
                "The StructureDefinition must contain snapshot.element."));
        }

        var differentialElements = definition.Differential?.Elements;
        if (differentialElements is null or { Count: 0 })
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.MissingDifferential,
                definition,
                sourceFile,
                "The StructureDefinition must contain differential.element."));
        }

        if (diagnostics.Count > 0)
        {
            return CreateResult([], diagnostics);
        }

        ValidateRoot(
            snapshotElements![0],
            definition.Type,
            "snapshot",
            definition,
            sourceFile,
            diagnostics);
        ValidateRoot(
            differentialElements![0],
            definition.Type,
            "differential",
            definition,
            sourceFile,
            diagnostics);

        var snapshotById = BuildSnapshotIndex(
            snapshotElements,
            definition,
            sourceFile,
            diagnostics);
        var differentialIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 1; index < differentialElements.Count; index++)
        {
            var candidate = differentialElements[index];
            var order = index - 1;

            if (!ValidateCandidateIdentity(
                    candidate,
                    differentialIds,
                    definition,
                    sourceFile,
                    diagnostics))
            {
                continue;
            }

            if (!snapshotById.TryGetValue(candidate.Id!, out var snapshotElement))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    definition,
                    sourceFile,
                    $"Differential element '{candidate.Id}' was not found in snapshot.element.",
                    candidate));
                continue;
            }

            var isSupported = ValidateMatchedElement(
                candidate,
                snapshotElement,
                definition,
                sourceFile,
                diagnostics);

            if (isSupported)
            {
                selectedElements.Add(
                    new SelectedElementDefinition(candidate, snapshotElement, order));
            }
        }

        if (diagnostics.Any(diagnostic =>
                diagnostic.Severity == GeneratorDiagnosticSeverity.Error))
        {
            return CreateResult([], diagnostics);
        }

        return CreateResult(selectedElements, diagnostics);
    }

    private static void ValidateRoot(
        ElementDefinitionDto root,
        string definitionType,
        string containerName,
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.Equals(root.Id, definitionType, StringComparison.Ordinal) &&
            string.Equals(root.Path, definitionType, StringComparison.Ordinal))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidInput,
            definition,
            sourceFile,
            $"The first {containerName}.element must be the root element " +
            $"'{definitionType}'.",
            root));
    }

    private static Dictionary<string, ElementDefinitionDto> BuildSnapshotIndex(
        IReadOnlyList<ElementDefinitionDto> snapshotElements,
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var snapshotById =
            new Dictionary<string, ElementDefinitionDto>(StringComparer.Ordinal);

        foreach (var snapshotElement in snapshotElements)
        {
            if (string.IsNullOrWhiteSpace(snapshotElement.Id))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    definition,
                    sourceFile,
                    "Every snapshot element must have a non-empty id.",
                    snapshotElement));
                continue;
            }

            if (!snapshotById.TryAdd(snapshotElement.Id, snapshotElement))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    definition,
                    sourceFile,
                    $"Duplicate snapshot element id '{snapshotElement.Id}'.",
                    snapshotElement));
            }
        }

        return snapshotById;
    }

    private static bool ValidateCandidateIdentity(
        ElementDefinitionDto candidate,
        ISet<string> differentialIds,
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(candidate.Id) ||
            string.IsNullOrWhiteSpace(candidate.Path))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                definition,
                sourceFile,
                "Every differential candidate must have a non-empty id and path.",
                candidate));
            return false;
        }

        if (!differentialIds.Add(candidate.Id))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                definition,
                sourceFile,
                $"Duplicate differential element id '{candidate.Id}'.",
                candidate));
            return false;
        }

        return true;
    }

    private static bool ValidateMatchedElement(
        ElementDefinitionDto candidate,
        ElementDefinitionDto snapshotElement,
        StructureDefinitionDto definition,
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var isSupported = true;

        if (!string.Equals(
                candidate.Path,
                snapshotElement.Path,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                definition,
                sourceFile,
                $"Differential element path '{candidate.Path}' does not match snapshot " +
                $"path '{snapshotElement.Path}' for id '{candidate.Id}'.",
                candidate));
            isSupported = false;
        }

        if (!TryGetDirectChildName(
                definition.Type!,
                candidate.Path!,
                out var childName))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedDefinition,
                definition,
                sourceFile,
                $"Element '{candidate.Id}' is not a direct child of " +
                $"'{definition.Type}'.",
                candidate));
            isSupported = false;
        }
        else if (MvpInheritedElementNames.Contains(childName))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedDefinition,
                definition,
                sourceFile,
                $"Inherited element override '{candidate.Id}' is not supported.",
                candidate));
            isSupported = false;
        }

        if (HasSlicing(candidate) ||
            HasSlicing(snapshotElement) ||
            candidate.Id!.Contains(':', StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedSlicing,
                definition,
                sourceFile,
                $"Slicing is not supported for element '{candidate.Id}'.",
                candidate));
            isSupported = false;
        }

        if (IsChoice(candidate) || IsChoice(snapshotElement))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedChoiceType,
                definition,
                sourceFile,
                $"Choice type is not supported for element '{candidate.Id}'.",
                candidate));
            isSupported = false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ContentReference) ||
            !string.IsNullOrWhiteSpace(snapshotElement.ContentReference))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedContentReference,
                definition,
                sourceFile,
                $"contentReference is not supported for element '{candidate.Id}'.",
                candidate));
            isSupported = false;
        }

        return isSupported;
    }

    private static bool TryGetDirectChildName(
        string definitionType,
        string elementPath,
        out string childName)
    {
        var prefix = $"{definitionType}.";
        if (!elementPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            childName = string.Empty;
            return false;
        }

        childName = elementPath[prefix.Length..];
        return childName.Length > 0 &&
               !childName.Contains('.', StringComparison.Ordinal);
    }

    private static bool HasSlicing(ElementDefinitionDto element)
    {
        return !string.IsNullOrWhiteSpace(element.SliceName) ||
               element.Slicing is { } slicing &&
               slicing.ValueKind is not JsonValueKind.Null and
                   not JsonValueKind.Undefined;
    }

    private static bool IsChoice(ElementDefinitionDto element)
    {
        return element.Id?.Contains("[x]", StringComparison.Ordinal) == true ||
               element.Path?.Contains("[x]", StringComparison.Ordinal) == true ||
               element.Types is { Count: > 1 };
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

    private static GenerationResult<IReadOnlyList<SelectedElementDefinition>> CreateResult(
        IReadOnlyList<SelectedElementDefinition> selectedElements,
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<IReadOnlyList<SelectedElementDefinition>>(
            selectedElements.ToArray(),
            diagnostics.ToArray());
    }
}
