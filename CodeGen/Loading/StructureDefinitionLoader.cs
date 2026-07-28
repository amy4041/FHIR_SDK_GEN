using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Loading;

public sealed class StructureDefinitionLoader
{
    public async Task<GenerationResult<IReadOnlyList<LoadedStructureDefinition>>> LoadAsync(
        string inputPath,
        string expectedFhirVersion,
        CancellationToken cancellationToken = default)
    {
        var definitions = new List<LoadedStructureDefinition>();
        var diagnostics = new List<GeneratorDiagnostic>();

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            diagnostics.Add(CreateInputDiagnostic(
                inputPath ?? string.Empty,
                "An input file or directory path is required."));
            return CreateResult(definitions, diagnostics);
        }

        if (string.IsNullOrWhiteSpace(expectedFhirVersion))
        {
            diagnostics.Add(CreateInputDiagnostic(
                inputPath,
                "An expected FHIR version is required."));
            return CreateResult(definitions, diagnostics);
        }

        var sourceFiles = ResolveSourceFiles(inputPath, diagnostics);
        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definition = await DeserializeAsync(
                sourceFile,
                diagnostics,
                cancellationToken);

            if (definition is null)
            {
                continue;
            }

            var diagnosticCountBeforeValidation = diagnostics.Count;
            ValidateDefinition(
                definition,
                sourceFile,
                expectedFhirVersion,
                diagnostics);

            if (diagnostics.Count == diagnosticCountBeforeValidation)
            {
                definitions.Add(new LoadedStructureDefinition(sourceFile, definition));
            }
        }

        return CreateResult(definitions, diagnostics);
    }

    private static IReadOnlyList<string> ResolveSourceFiles(
        string inputPath,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        try
        {
            var fullInputPath = Path.GetFullPath(inputPath);

            if (File.Exists(fullInputPath))
            {
                return [fullInputPath];
            }

            if (Directory.Exists(fullInputPath))
            {
                return Directory
                    .EnumerateFiles(
                        fullInputPath,
                        "*.json",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            }

            diagnostics.Add(CreateInputDiagnostic(
                fullInputPath,
                $"The input path does not exist: '{fullInputPath}'."));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            diagnostics.Add(CreateInputDiagnostic(
                inputPath,
                $"The input path could not be accessed: {exception.Message}"));
        }

        return [];
    }

    private static async Task<StructureDefinitionDto?> DeserializeAsync(
        string sourceFile,
        ICollection<GeneratorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(sourceFile);
            var definition =
                await JsonSerializer.DeserializeAsync<StructureDefinitionDto>(
                    stream,
                    cancellationToken: cancellationToken);

            if (definition is null)
            {
                diagnostics.Add(CreateInputDiagnostic(
                    sourceFile,
                    "The JSON document contains no StructureDefinition."));
            }

            return definition;
        }
        catch (JsonException exception)
        {
            diagnostics.Add(CreateInputDiagnostic(
                sourceFile,
                $"The JSON document could not be deserialized: {exception.Message}"));
        }
        catch (Exception exception) when (
            exception is IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            diagnostics.Add(CreateInputDiagnostic(
                sourceFile,
                $"The JSON file could not be read: {exception.Message}"));
        }

        return null;
    }

    private static void ValidateDefinition(
        StructureDefinitionDto definition,
        string sourceFile,
        string expectedFhirVersion,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.Equals(
                definition.ResourceType,
                "StructureDefinition",
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                sourceFile,
                definition,
                "The resourceType must be 'StructureDefinition'."));
        }

        RequireText(definition.Id, "id", sourceFile, definition, diagnostics);
        RequireText(definition.Url, "url", sourceFile, definition, diagnostics);
        RequireText(definition.Version, "version", sourceFile, definition, diagnostics);
        RequireText(definition.Name, "name", sourceFile, definition, diagnostics);
        RequireText(definition.Type, "type", sourceFile, definition, diagnostics);
        RequireText(definition.Kind, "kind", sourceFile, definition, diagnostics);
        RequireText(
            definition.BaseDefinition,
            "baseDefinition",
            sourceFile,
            definition,
            diagnostics);
        RequireText(
            definition.Derivation,
            "derivation",
            sourceFile,
            definition,
            diagnostics);

        if (definition.IsAbstract is null)
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput,
                sourceFile,
                definition,
                "The required field 'abstract' is missing."));
        }

        if (!string.IsNullOrWhiteSpace(definition.Version) &&
            !string.Equals(
                definition.Version,
                expectedFhirVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.FhirVersionMismatch,
                sourceFile,
                definition,
                $"FHIR version '{definition.Version}' does not match expected version " +
                $"'{expectedFhirVersion}'."));
        }

        if (definition.Snapshot?.Elements is null or { Count: 0 })
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.MissingSnapshot,
                sourceFile,
                definition,
                "The StructureDefinition must contain snapshot.element."));
        }

        if (definition.Differential?.Elements is null or { Count: 0 })
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.MissingDifferential,
                sourceFile,
                definition,
                "The StructureDefinition must contain differential.element."));
        }

        if (!string.IsNullOrWhiteSpace(definition.Kind) &&
            !string.Equals(
                definition.Kind,
                "complex-type",
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedDefinition,
                sourceFile,
                definition,
                $"StructureDefinition kind '{definition.Kind}' is not supported."));
        }

        if (!string.IsNullOrWhiteSpace(definition.Derivation) &&
            !string.Equals(
                definition.Derivation,
                "specialization",
                StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDefinitionDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedDefinition,
                sourceFile,
                definition,
                $"StructureDefinition derivation '{definition.Derivation}' is not supported."));
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

        diagnostics.Add(CreateDefinitionDiagnostic(
            GeneratorDiagnosticCodes.InvalidInput,
            sourceFile,
            definition,
            $"The required field '{fieldName}' is missing."));
    }

    private static GeneratorDiagnostic CreateInputDiagnostic(
        string sourceFile,
        string message)
    {
        return new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.InvalidInput,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceFile);
    }

    private static GeneratorDiagnostic CreateDefinitionDiagnostic(
        string code,
        string sourceFile,
        StructureDefinitionDto definition,
        string message)
    {
        return new GeneratorDiagnostic(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceFile,
            definition.Url,
            definition.Version);
    }

    private static GenerationResult<IReadOnlyList<LoadedStructureDefinition>> CreateResult(
        IReadOnlyList<LoadedStructureDefinition> definitions,
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<IReadOnlyList<LoadedStructureDefinition>>(
            definitions.ToArray(),
            diagnostics.ToArray());
    }
}
