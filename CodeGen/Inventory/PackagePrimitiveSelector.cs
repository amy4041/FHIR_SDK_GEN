using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;

namespace MyFhirSdk.CodeGen.Inventory;

/// <summary>Selects primitive specializations from an identity-validated package.</summary>
public sealed class PackagePrimitiveSelector
{
    public GenerationResult<PrimitiveDefinitionInventory?> Select(
        LoadedDefinitionPackage package,
        string expectedFhirVersion)
    {
        ArgumentNullException.ThrowIfNull(package);
        var diagnostics = new List<GeneratorDiagnostic>();
        var selected = new List<LoadedStructureDefinition>();
        if (string.IsNullOrWhiteSpace(expectedFhirVersion))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                GeneratorDiagnosticSeverity.Error,
                "An expected FHIR version is required.", "package/package.json"));
            return Failure(diagnostics);
        }
        if (!string.Equals(package.Identity.FhirVersion, expectedFhirVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageIdentityMismatch,
                GeneratorDiagnosticSeverity.Error,
                $"Package FHIR version '{package.Identity.FhirVersion}' does not match expected version '{expectedFhirVersion}'.",
                "package/package.json"));
            return Failure(diagnostics);
        }

        foreach (var entry in package.Definitions.OrderBy(item => item.SourceFile, StringComparer.Ordinal))
        {
            var definition = entry.Definition;
            var primitiveShaped = definition.Kind == "primitive-type" ||
                definition.BaseDefinition == "http://hl7.org/fhir/StructureDefinition/PrimitiveType";
            // Only recognized non-primitive categories may bypass primitive validation.
            // In particular, xhtml derives from Element rather than PrimitiveType,
            // so an unknown kind must not make that entry silently disappear.
            if (!primitiveShaped &&
                definition.Kind is "complex-type" or "resource" or "logical") continue;

            void Error(string code, string message) => diagnostics.Add(new GeneratorDiagnostic(
                code, GeneratorDiagnosticSeverity.Error, message,
                entry.SourceFile, definition.Url, definition.Version));
            void Require(string? value, string field)
            {
                if (string.IsNullOrWhiteSpace(value)) Error(
                    GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                    $"The required primitive field '{field}' is missing.");
            }

            // Constraints are outside generation scope, but must retain basic identity.
            // Do not impose specialization version/snapshot rules on profiles.
            if (definition.ResourceType == "StructureDefinition" &&
                definition.Kind == "primitive-type" && definition.Derivation == "constraint")
            {
                Require(definition.Id, "id");
                Require(definition.Url, "url");
                Require(definition.Type, "type");
                Require(definition.BaseDefinition, "baseDefinition");
                continue;
            }

            Require(definition.Id, "id");
            Require(definition.Derivation, "derivation");
            if (definition.Kind != "primitive-type") Error(
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                "The primitive StructureDefinition kind must be 'primitive-type'.");
            if (!string.IsNullOrWhiteSpace(definition.Derivation) && definition.Derivation != "specialization") Error(
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
                "The primitive StructureDefinition derivation must be 'specialization'.");
            if (definition.IsAbstract is null) Require(null, "abstract");
            if (definition.FhirVersion is not null && definition.FhirVersion != expectedFhirVersion) Error(
                GeneratorDiagnosticCodes.FhirVersionMismatch,
                $"FHIR version '{definition.FhirVersion}' does not match expected version '{expectedFhirVersion}'.");
            if (definition.Snapshot?.Elements is null or { Count: 0 }) Error(
                GeneratorDiagnosticCodes.MissingSnapshot,
                "The primitive StructureDefinition must contain snapshot.element.");
            if (definition.Differential?.Elements is null or { Count: 0 }) Error(
                GeneratorDiagnosticCodes.MissingDifferential,
                "The primitive StructureDefinition must contain differential.element.");
            selected.Add(entry);
        }

        if (selected.Count == 0) diagnostics.Add(new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitiveInventory,
            GeneratorDiagnosticSeverity.Error,
            "The definition package contains no primitive specializations.", "package/package.json"));

        // Keep identity and duplicate checks owned by the existing primitive inventory builder.
        var inventory = new PrimitiveDefinitionInventoryBuilder().Build(selected, expectedFhirVersion);
        diagnostics.AddRange(inventory.Diagnostics);
        return diagnostics.Count > 0 ? Failure(diagnostics) : inventory;
    }

    private static GenerationResult<PrimitiveDefinitionInventory?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) => new(null, diagnostics
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.SourceFile, StringComparer.Ordinal)
            .ThenBy(item => item.DefinitionCanonical, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToArray());
}
