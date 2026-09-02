using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class DefinitionInventoryBuilder
{
    private const string StructureDefinitionResourceType = "StructureDefinition";

    public GenerationResult<DefinitionInventory?> Build(
        LoadedDefinitionPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var diagnostics = new List<GeneratorDiagnostic>();
        ValidatePackageIdentity(package.Identity, diagnostics);
        if (package.Definitions.Count == 0)
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidDefinitionInventory,
                GeneratorDiagnosticSeverity.Error,
                "The definition inventory input is empty.",
                "<definition-inventory>"));
        }

        var items = new List<DefinitionInventoryItem>();
        foreach (var loadedDefinition in package.Definitions)
        {
            var item = CreateItem(
                loadedDefinition,
                package.Identity.FhirVersion,
                diagnostics);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        ValidateDuplicates(
            items,
            item => item.SourceIdentity,
            "source identity",
            include: static _ => true,
            diagnostics);
        ValidateDuplicates(
            items,
            item => item.Canonical,
            "canonical",
            include: static _ => true,
            diagnostics);
        ValidateDuplicates(
            items,
            item => item.FhirTypeName,
            "specialization FHIR type",
            include: static item => item.Category is
                DefinitionInventoryCategory.ModelRoot or
                DefinitionInventoryCategory.ModelSpecialization or
                DefinitionInventoryCategory.PrimitiveSpecialization,
            diagnostics);

        if (diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        var inventory = new DefinitionInventory(
            package.Identity,
            items
                .OrderBy(item => item.Canonical, StringComparer.Ordinal)
                .ThenBy(item => item.SourceIdentity, StringComparer.Ordinal));

        return new GenerationResult<DefinitionInventory?>(
            inventory,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static DefinitionInventoryItem? CreateItem(
        LoadedStructureDefinition loadedDefinition,
        string expectedFhirVersion,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(loadedDefinition);
        ArgumentNullException.ThrowIfNull(loadedDefinition.Definition);

        var definition = loadedDefinition.Definition;
        var sourceIdentity = loadedDefinition.SourceFile;
        if (string.IsNullOrWhiteSpace(sourceIdentity))
        {
            sourceIdentity = "<definition-inventory>";
            diagnostics.Add(CreateDiagnostic(
                sourceIdentity,
                definition,
                "A package entry source identity is required."));
        }

        var category = Classify(definition, sourceIdentity, diagnostics);
        ValidateCommonIdentity(definition, sourceIdentity, diagnostics);
        if (category is not null)
        {
            ValidateCategory(
                definition,
                category.Value,
                expectedFhirVersion,
                sourceIdentity,
                diagnostics);
        }

        if (category is null ||
            string.IsNullOrWhiteSpace(definition.Id) ||
            string.IsNullOrWhiteSpace(definition.Type) ||
            string.IsNullOrWhiteSpace(definition.Url) ||
            string.IsNullOrWhiteSpace(definition.Kind) ||
            definition.IsAbstract is null)
        {
            return null;
        }

        return new DefinitionInventoryItem(
            sourceIdentity,
            definition.Id,
            definition.Type,
            definition.Url,
            definition.Version,
            definition.FhirVersion ?? expectedFhirVersion,
            definition.Kind,
            definition.IsAbstract.Value,
            definition.BaseDefinition,
            definition.Derivation,
            category.Value,
            definition);
    }

    private static DefinitionInventoryCategory? Classify(
        StructureDefinitionDto definition,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.Equals(definition.Derivation, "specialization", StringComparison.Ordinal))
        {
            return definition.Kind switch
            {
                "complex-type" or "resource" =>
                    DefinitionInventoryCategory.ModelSpecialization,
                "primitive-type" =>
                    DefinitionInventoryCategory.PrimitiveSpecialization,
                _ => UnsupportedCategory(definition, sourceIdentity, diagnostics)
            };
        }

        if (string.Equals(definition.Derivation, "constraint", StringComparison.Ordinal))
        {
            return definition.Kind is "complex-type" or "resource"
                ? DefinitionInventoryCategory.ConstraintProfile
                : UnsupportedCategory(definition, sourceIdentity, diagnostics);
        }

        if (string.IsNullOrWhiteSpace(definition.Derivation))
        {
            if (string.Equals(definition.Kind, "logical", StringComparison.Ordinal))
            {
                return DefinitionInventoryCategory.LogicalModel;
            }

            if (string.Equals(definition.Kind, "complex-type", StringComparison.Ordinal) &&
                string.Equals(definition.Type, "Base", StringComparison.Ordinal))
            {
                return DefinitionInventoryCategory.ModelRoot;
            }
        }

        return UnsupportedCategory(definition, sourceIdentity, diagnostics);
    }

    private static DefinitionInventoryCategory? UnsupportedCategory(
        StructureDefinitionDto definition,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        diagnostics.Add(new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.UnsupportedDefinition,
            GeneratorDiagnosticSeverity.Error,
            $"StructureDefinition kind '{definition.Kind ?? "<missing>"}' and derivation " +
            $"'{definition.Derivation ?? "<missing>"}' do not have an approved C1 category.",
            sourceIdentity,
            definition.Url,
            definition.Version));
        return null;
    }

    private static void ValidateCommonIdentity(
        StructureDefinitionDto definition,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.Equals(
                definition.ResourceType,
                StructureDefinitionResourceType,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                sourceIdentity,
                definition,
                "The resourceType must be 'StructureDefinition'."));
        }

        RequireText(definition.Id, "id", sourceIdentity, definition, diagnostics);
        RequireText(definition.Type, "type", sourceIdentity, definition, diagnostics);
        RequireText(definition.Url, "url", sourceIdentity, definition, diagnostics);
        RequireText(definition.Kind, "kind", sourceIdentity, definition, diagnostics);

        if (definition.IsAbstract is null)
        {
            diagnostics.Add(CreateDiagnostic(
                sourceIdentity,
                definition,
                "The required field 'abstract' is missing."));
        }
    }

    private static void ValidateCategory(
        StructureDefinitionDto definition,
        DefinitionInventoryCategory category,
        string expectedFhirVersion,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (category is DefinitionInventoryCategory.ModelRoot or
            DefinitionInventoryCategory.ModelSpecialization or
            DefinitionInventoryCategory.PrimitiveSpecialization)
        {
            RequireText(
                definition.Version,
                "version",
                sourceIdentity,
                definition,
                diagnostics);
            RequireText(
                definition.FhirVersion,
                "fhirVersion",
                sourceIdentity,
                definition,
                diagnostics);
            if (category is not DefinitionInventoryCategory.ModelRoot)
            {
                RequireText(
                    definition.BaseDefinition,
                    "baseDefinition",
                    sourceIdentity,
                    definition,
                    diagnostics);
            }
            RequireElements(
                definition.Snapshot?.Elements,
                "snapshot.element",
                GeneratorDiagnosticCodes.MissingSnapshot,
                sourceIdentity,
                definition,
                diagnostics);
            RequireElements(
                definition.Differential?.Elements,
                "differential.element",
                GeneratorDiagnosticCodes.MissingDifferential,
                sourceIdentity,
                definition,
                diagnostics);
            ValidateFhirVersion(
                definition,
                expectedFhirVersion,
                sourceIdentity,
                diagnostics);
            return;
        }

        if (!string.IsNullOrWhiteSpace(definition.FhirVersion) &&
            !string.Equals(
                definition.FhirVersion,
                expectedFhirVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateVersionDiagnostic(
                sourceIdentity,
                definition,
                definition.FhirVersion,
                expectedFhirVersion,
                "fhirVersion"));
        }
    }

    private static void ValidateFhirVersion(
        StructureDefinitionDto definition,
        string expectedFhirVersion,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(definition.FhirVersion) &&
            !string.Equals(
                definition.FhirVersion,
                expectedFhirVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateVersionDiagnostic(
                sourceIdentity,
                definition,
                definition.FhirVersion,
                expectedFhirVersion,
                "fhirVersion"));
        }

        if (!string.IsNullOrWhiteSpace(definition.Version) &&
            !string.Equals(
                definition.Version,
                expectedFhirVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateVersionDiagnostic(
                sourceIdentity,
                definition,
                definition.Version,
                expectedFhirVersion,
                "version"));
        }
    }

    private static GeneratorDiagnostic CreateVersionDiagnostic(
        string sourceIdentity,
        StructureDefinitionDto definition,
        string? actual,
        string expected,
        string fieldName) =>
        new(
            GeneratorDiagnosticCodes.FhirVersionMismatch,
            GeneratorDiagnosticSeverity.Error,
            $"StructureDefinition {fieldName} '{actual}' does not match package FHIR " +
            $"version '{expected}'.",
            sourceIdentity,
            definition.Url,
            definition.Version);

    private static void RequireText(
        string? value,
        string fieldName,
        string sourceIdentity,
        StructureDefinitionDto definition,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            sourceIdentity,
            definition,
            $"The required inventory field '{fieldName}' is missing."));
    }

    private static void RequireElements(
        IReadOnlyList<ElementDefinitionDto>? elements,
        string fieldName,
        string diagnosticCode,
        string sourceIdentity,
        StructureDefinitionDto definition,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (elements is { Count: > 0 })
        {
            return;
        }

        diagnostics.Add(new GeneratorDiagnostic(
            diagnosticCode,
            GeneratorDiagnosticSeverity.Error,
            $"The selected StructureDefinition must contain {fieldName}.",
            sourceIdentity,
            definition.Url,
            definition.Version));
    }

    private static void ValidatePackageIdentity(
        DefinitionPackageIdentity identity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        RequirePackageText(identity.PackageId, "package id", diagnostics);
        RequirePackageText(identity.PackageVersion, "package version", diagnostics);
        RequirePackageText(identity.PackageType, "package type", diagnostics);
        RequirePackageText(identity.FhirVersion, "FHIR version", diagnostics);
    }

    private static void RequirePackageText(
        string? value,
        string fieldName,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        diagnostics.Add(new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.InvalidDefinitionInventory,
            GeneratorDiagnosticSeverity.Error,
            $"A definition inventory {fieldName} is required.",
            "<definition-inventory>"));
    }

    private static void ValidateDuplicates(
        IEnumerable<DefinitionInventoryItem> items,
        Func<DefinitionInventoryItem, string> keySelector,
        string fieldName,
        Func<DefinitionInventoryItem, bool> include,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var groups = items
            .Where(include)
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(item => item.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(item => item.Canonical, StringComparer.Ordinal)
                .ToArray();
            var firstSource = ordered[0].SourceIdentity;

            foreach (var duplicate in ordered.Skip(1))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.DuplicateDefinitionInventoryEntry,
                    GeneratorDiagnosticSeverity.Error,
                    $"Definition inventory {fieldName} '{group.Key}' is duplicated; " +
                    $"the first ordinal source is '{firstSource}'.",
                    duplicate.SourceIdentity,
                    duplicate.Canonical,
                    duplicate.DefinitionVersion));
            }
        }
    }

    private static GeneratorDiagnostic CreateDiagnostic(
        string sourceIdentity,
        StructureDefinitionDto definition,
        string message) =>
        new(
            GeneratorDiagnosticCodes.InvalidDefinitionInventory,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceIdentity,
            definition.Url,
            definition.Version);

    private static GenerationResult<DefinitionInventory?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(
            null,
            diagnostics
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.DefinitionCanonical, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToArray());
}
