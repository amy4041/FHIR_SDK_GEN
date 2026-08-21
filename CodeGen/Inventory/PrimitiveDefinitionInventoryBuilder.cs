using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class PrimitiveDefinitionInventoryBuilder
{
    private const string StructureDefinitionResourceType = "StructureDefinition";
    private const string PrimitiveTypeKind = "primitive-type";

    public GenerationResult<PrimitiveDefinitionInventory?> Build(
        IReadOnlyList<LoadedStructureDefinition> definitions,
        string expectedFhirVersion)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var diagnostics = new List<GeneratorDiagnostic>();
        if (string.IsNullOrWhiteSpace(expectedFhirVersion))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                "<primitive-inventory>",
                null,
                "An expected FHIR version is required."));
            return CreateFailure(diagnostics);
        }

        var items = new List<PrimitiveDefinitionInventoryItem>();
        foreach (var loadedDefinition in definitions)
        {
            var item = CreateItem(
                loadedDefinition,
                expectedFhirVersion,
                diagnostics);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        ValidateDuplicates(
            items,
            item => item.FhirTypeName,
            "FHIR type name",
            diagnostics);
        ValidateDuplicates(
            items,
            item => item.Canonical,
            "canonical",
            diagnostics);

        var orderedDiagnostics = OrderDiagnostics(diagnostics);
        if (orderedDiagnostics.Length > 0)
        {
            return CreateFailure(orderedDiagnostics);
        }

        var inventory = new PrimitiveDefinitionInventory(
            expectedFhirVersion,
            items.OrderBy(item => item.FhirTypeName, StringComparer.Ordinal));

        return new GenerationResult<PrimitiveDefinitionInventory?>(
            inventory,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static PrimitiveDefinitionInventoryItem? CreateItem(
        LoadedStructureDefinition loadedDefinition,
        string expectedFhirVersion,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(loadedDefinition);

        var definition = loadedDefinition.Definition;
        ArgumentNullException.ThrowIfNull(definition);

        var sourceFile = loadedDefinition.SourceFile;
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            sourceFile = "<primitive-definition>";
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                sourceFile,
                definition,
                "A primitive StructureDefinition source file is required."));
        }

        ValidateIdentity(
            definition,
            sourceFile,
            expectedFhirVersion,
            diagnostics);

        if (string.IsNullOrWhiteSpace(definition.Type) ||
            string.IsNullOrWhiteSpace(definition.Url) ||
            string.IsNullOrWhiteSpace(definition.Version) ||
            string.IsNullOrWhiteSpace(definition.BaseDefinition) ||
            string.IsNullOrWhiteSpace(definition.Name))
        {
            return null;
        }

        return new PrimitiveDefinitionInventoryItem(
            sourceFile,
            definition.Type,
            definition.Url,
            definition.Version,
            definition.BaseDefinition,
            definition.Name,
            definition.Description);
    }

    private static void ValidateIdentity(
        StructureDefinitionDto definition,
        string sourceFile,
        string expectedFhirVersion,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.Equals(
                definition.ResourceType,
                StructureDefinitionResourceType,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                sourceFile,
                definition,
                "The resourceType must be 'StructureDefinition'."));
        }

        if (!string.Equals(
                definition.Kind,
                PrimitiveTypeKind,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedDefinition,
                sourceFile,
                definition,
                $"StructureDefinition kind '{definition.Kind}' is not a primitive type."));
        }

        RequireText(definition.Type, "type", sourceFile, definition, diagnostics);
        RequireText(definition.Url, "url", sourceFile, definition, diagnostics);
        RequireText(
            definition.Version,
            "version",
            sourceFile,
            definition,
            diagnostics);
        RequireText(
            definition.BaseDefinition,
            "baseDefinition",
            sourceFile,
            definition,
            diagnostics);
        RequireText(definition.Name, "name", sourceFile, definition, diagnostics);

        if (!string.IsNullOrWhiteSpace(definition.Version) &&
            !string.Equals(
                definition.Version,
                expectedFhirVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.FhirVersionMismatch,
                sourceFile,
                definition,
                $"FHIR version '{definition.Version}' does not match expected version " +
                $"'{expectedFhirVersion}'."));
        }

    }

    private static void RequireText(
        string? value,
        string fieldName,
        string sourceFile,
        StructureDefinitionDto definition,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
            sourceFile,
            definition,
            $"The required primitive field '{fieldName}' is missing."));
    }

    private static void ValidateDuplicates(
        IEnumerable<PrimitiveDefinitionInventoryItem> items,
        Func<PrimitiveDefinitionInventoryItem, string> keySelector,
        string fieldName,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var duplicateGroups = items
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in duplicateGroups)
        {
            var orderedItems = group
                .OrderBy(item => item.SourceFile, StringComparer.Ordinal)
                .ThenBy(item => item.FhirTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.Canonical, StringComparer.Ordinal)
                .ToArray();
            var firstSource = orderedItems[0].SourceFile;

            foreach (var duplicate in orderedItems.Skip(1))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry,
                    GeneratorDiagnosticSeverity.Error,
                    $"Primitive inventory {fieldName} '{group.Key}' is duplicated; " +
                    $"the first ordinal source is '{firstSource}'.",
                    duplicate.SourceFile,
                    duplicate.Canonical,
                    duplicate.FhirVersion));
            }
        }
    }

    private static GeneratorDiagnostic CreateDiagnostic(
        string code,
        string sourceFile,
        StructureDefinitionDto? definition,
        string message)
    {
        return new GeneratorDiagnostic(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceFile,
            definition?.Url,
            definition?.Version);
    }

    private static GeneratorDiagnostic[] OrderDiagnostics(
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        return diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
            .ThenBy(
                diagnostic => diagnostic.DefinitionCanonical,
                StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static GenerationResult<PrimitiveDefinitionInventory?> CreateFailure(
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<PrimitiveDefinitionInventory?>(
            null,
            OrderDiagnostics(diagnostics));
    }
}
