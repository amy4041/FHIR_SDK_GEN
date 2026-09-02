using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Loading;

public sealed class DefinitionPackageLoader
{
    private const string PackageDocumentEntry = "package/package.json";

    public async Task<GenerationResult<LoadedDefinitionPackage?>> LoadAsync(
        IDefinitionPackageInput input,
        DefinitionPackageLoadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<GeneratorDiagnostic>();
        if (!ValidateOptions(options, input.SourceIdentity, diagnostics))
        {
            return Failure(diagnostics);
        }

        DefinitionPackageDocumentDto? packageDocument = null;
        var definitions = new List<LoadedStructureDefinition>();

        try
        {
            await using var archive = await input.OpenReadAsync(cancellationToken);
            using var gzip = new GZipStream(archive, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);

            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.DataStream is null)
                {
                    continue;
                }

                if (string.Equals(entry.Name, PackageDocumentEntry, StringComparison.Ordinal))
                {
                    if (packageDocument is not null)
                    {
                        diagnostics.Add(CreateDiagnostic(
                            GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                            entry.Name,
                            "The package archive contains more than one package/package.json entry."));
                        continue;
                    }

                    packageDocument = Deserialize<DefinitionPackageDocumentDto>(
                        entry.DataStream,
                        entry.Name,
                        diagnostics);
                    continue;
                }

                if (!IsPackageJsonEntry(entry.Name))
                {
                    continue;
                }

                var definition = DeserializeStructureDefinition(
                    entry.DataStream,
                    entry.Name,
                    diagnostics);
                if (definition is not null)
                {
                    definitions.Add(new LoadedStructureDefinition(entry.Name, definition));
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidDataException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                input.SourceIdentity,
                $"The definition package could not be read: {exception.Message}"));
        }

        if (packageDocument is null)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                input.SourceIdentity,
                "The definition package must contain package/package.json."));
        }
        else
        {
            ValidateIdentity(packageDocument, options, diagnostics);
        }

        if (definitions.Count == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                input.SourceIdentity,
                "The definition package contains no StructureDefinition entries."));
        }

        if (diagnostics.Count > 0 || packageDocument is null)
        {
            return Failure(diagnostics);
        }

        var identity = new DefinitionPackageIdentity(
            packageDocument.Name!,
            packageDocument.Version!,
            packageDocument.Type!,
            options.FhirVersion);
        var package = new LoadedDefinitionPackage(
            identity,
            definitions.OrderBy(
                definition => definition.SourceFile,
                StringComparer.Ordinal));

        return new GenerationResult<LoadedDefinitionPackage?>(
            package,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static T? Deserialize<T>(
        Stream stream,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
        where T : class
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(stream);
            if (value is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                    sourceIdentity,
                    "The JSON entry contains no document."));
            }

            return value;
        }
        catch (JsonException exception)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                sourceIdentity,
                $"The JSON entry could not be deserialized: {exception.Message}"));
            return null;
        }
    }

    private static StructureDefinitionDto? DeserializeStructureDefinition(
        Stream stream,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        try
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("resourceType", out var resourceType) ||
                !string.Equals(
                    resourceType.GetString(),
                    "StructureDefinition",
                    StringComparison.Ordinal))
            {
                return null;
            }

            return root.Deserialize<StructureDefinitionDto>();
        }
        catch (JsonException exception)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                sourceIdentity,
                $"The JSON entry could not be deserialized: {exception.Message}"));
            return null;
        }
    }

    private static bool ValidateOptions(
        DefinitionPackageLoadOptions options,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var valid = true;
        valid &= RequireOption(options.PackageId, "package id", sourceIdentity, diagnostics);
        valid &= RequireOption(options.PackageVersion, "package version", sourceIdentity, diagnostics);
        valid &= RequireOption(options.FhirVersion, "FHIR version", sourceIdentity, diagnostics);
        valid &= RequireOption(options.PackageType, "package type", sourceIdentity, diagnostics);
        return valid;
    }

    private static bool RequireOption(
        string? value,
        string fieldName,
        string sourceIdentity,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.DefinitionPackageIdentityMismatch,
            sourceIdentity,
            $"An expected {fieldName} is required."));
        return false;
    }

    private static void ValidateIdentity(
        DefinitionPackageDocumentDto document,
        DefinitionPackageLoadOptions expected,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        CompareIdentity("package id", document.Name, expected.PackageId, diagnostics);
        CompareIdentity("package version", document.Version, expected.PackageVersion, diagnostics);
        CompareIdentity("package type", document.Type, expected.PackageType, diagnostics);

        if (document.FhirVersions is null ||
            !document.FhirVersions.Contains(expected.FhirVersion, StringComparer.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.DefinitionPackageIdentityMismatch,
                PackageDocumentEntry,
                $"Package FHIR versions do not contain expected version '{expected.FhirVersion}'."));
        }
    }

    private static void CompareIdentity(
        string fieldName,
        string? actual,
        string expected,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.DefinitionPackageIdentityMismatch,
            PackageDocumentEntry,
            $"Package {fieldName} '{actual ?? "<missing>"}' does not match expected " +
            $"value '{expected}'."));
    }

    private static bool IsPackageJsonEntry(string name) =>
        name.StartsWith("package/", StringComparison.Ordinal) &&
        name.EndsWith(".json", StringComparison.Ordinal);

    private static GeneratorDiagnostic CreateDiagnostic(
        string code,
        string sourceIdentity,
        string message) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceIdentity);

    private static GenerationResult<LoadedDefinitionPackage?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(
            null,
            diagnostics
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToArray());
}
