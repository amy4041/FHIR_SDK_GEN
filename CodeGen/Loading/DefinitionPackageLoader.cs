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
        var entryNames = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            await using var archive = await input.OpenReadAsync(cancellationToken);
            using var gzip = new GZipStream(archive, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);

            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var logicalName = entry.EntryType == TarEntryType.Directory && entry.Name.EndsWith('/')
                    ? entry.Name[..^1] : entry.Name;
                if (!IsCanonicalEntryName(logicalName))
                {
                    diagnostics.Add(CreateDiagnostic(
                        GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                        entry.Name, "Archive entry must use a canonical relative package/ path."));
                    continue;
                }
                if (!entryNames.Add(logicalName))
                {
                    // Neither copy of an ambiguous entry may determine inventory or
                    // diagnostics. This also handles a corrupt first package.json.
                    definitions.RemoveAll(definition => definition.SourceFile == logicalName);
                    diagnostics.RemoveAll(diagnostic => diagnostic.SourceFile == logicalName);
                    if (logicalName == PackageDocumentEntry) packageDocument = null;
                    diagnostics.Add(CreateDiagnostic(
                        GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                        entry.Name, "The package archive contains a duplicate logical entry."));
                    continue;
                }
                if (entry.EntryType == TarEntryType.Directory) continue;
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    diagnostics.Add(CreateDiagnostic(
                        GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                        entry.Name, "The package archive must not contain links or special files."));
                    continue;
                }
                if (entry.DataStream is null)
                {
                    continue;
                }

                if (string.Equals(entry.Name, PackageDocumentEntry, StringComparison.Ordinal))
                {
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
            // TarReader can stop at the tar terminator before gzip has checked its trailer.
            // The host enables System.IO.Compression.UseStrictValidation at startup:
            // without it, reaching EOF with a missing trailer does not throw.
            await gzip.CopyToAsync(Stream.Null, cancellationToken);
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
            if (root.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.DefinitionPackageReadFailure,
                    sourceIdentity,
                    "The JSON entry must contain an object."));
                return null;
            }
            // Preserve primitive-shaped entries for selector validation even when
            // resourceType is missing or incorrect; filtering here would hide corruption.
            var primitiveShaped = HasString(root, "kind", "primitive-type") ||
                HasString(root, "baseDefinition", "http://hl7.org/fhir/StructureDefinition/PrimitiveType");
            if (!HasString(root, "resourceType", "StructureDefinition") && !primitiveShaped)
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

    private static bool HasString(JsonElement element, string name, string expected) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

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

    private static bool IsCanonicalEntryName(string name) =>
        (name == "package" || name.StartsWith("package/", StringComparison.Ordinal)) &&
        !name.Contains('\\') && !name.Contains(':') &&
        name.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));

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
