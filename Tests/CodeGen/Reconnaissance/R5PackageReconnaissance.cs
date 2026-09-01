using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace MyFhirSdk.CodeGen.Tests.Reconnaissance;

internal static class R5PackageReconnaissance
{
    private const string MissingValue = "<missing>";

    internal static R5PackageReconnaissanceInput Read(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        string archiveSha256;
        using (var archive = File.OpenRead(archivePath))
        {
            archiveSha256 = Convert
                .ToHexString(SHA256.HashData(archive))
                .ToLowerInvariant();
        }

        PackageObservation? package = null;
        var definitions = new List<DefinitionObservation>();

        using var archiveStream = File.OpenRead(archivePath);
        using var gzip = new GZipStream(
            archiveStream,
            CompressionMode.Decompress);
        using var reader = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            var sourceIdentity = entry.Name.Replace('\\', '/');
            if (entry.DataStream is null ||
                !sourceIdentity.StartsWith("package/", StringComparison.Ordinal) ||
                !sourceIdentity.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(entry.DataStream);
            var root = document.RootElement;

            if (string.Equals(
                    sourceIdentity,
                    "package/package.json",
                    StringComparison.Ordinal))
            {
                package = ReadPackage(root);
                continue;
            }

            if (!string.Equals(
                    GetOptionalString(root, "resourceType"),
                    "StructureDefinition",
                    StringComparison.Ordinal))
            {
                continue;
            }

            definitions.Add(ReadDefinition(sourceIdentity, root));
        }

        return new R5PackageReconnaissanceInput(
            package ?? throw new InvalidOperationException(
                "The package archive does not contain package/package.json."),
            archiveSha256,
            definitions);
    }

    internal static R5PackageReconnaissanceReport Build(
        R5PackageReconnaissanceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var definitions = input.Definitions.ToArray();
        var specializations = definitions
            .Where(definition => string.Equals(
                definition.Derivation,
                "specialization",
                StringComparison.Ordinal))
            .ToArray();

        return new R5PackageReconnaissanceReport(
            SchemaVersion: 1,
            Package: new PackageReport(
                input.Package.PackageId,
                input.Package.PackageVersion,
                input.Package.PackageType,
                input.Package.License,
                input.Package.FhirVersions
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                input.ArchiveSha256),
            Definitions: new DefinitionReport(
                Total: definitions.Length,
                ByKind: CountBy(
                    definitions,
                    definition => definition.Kind),
                ByDerivation: CountBy(
                    definitions,
                    definition => definition.Derivation),
                ByKindAndDerivation: CountBy(
                    definitions,
                    definition =>
                        $"{Normalize(definition.Kind)}|" +
                        Normalize(definition.Derivation)),
                ByVersion: CountBy(
                    definitions,
                    definition => definition.Version),
                AbstractCount: definitions.Count(
                    definition => definition.IsAbstract is true),
                ConcreteCount: definitions.Count(
                    definition => definition.IsAbstract is false),
                MissingAbstractFlagCount: definitions.Count(
                    definition => definition.IsAbstract is null),
                MissingDerivationSources: GetSources(
                    definitions,
                    definition => string.IsNullOrWhiteSpace(
                        definition.Derivation)),
                MissingBaseDefinitionSources: GetSources(
                    definitions,
                    definition => string.IsNullOrWhiteSpace(
                        definition.BaseDefinition)),
                MissingSnapshotSources: GetSources(
                    definitions,
                    definition => definition.IsSnapshotMissing),
                SpecializationMissingSnapshotSources: GetSources(
                    specializations,
                    definition => definition.IsSnapshotMissing)),
            ShapeUsage: SumShapeMetrics(definitions),
            ShapeUsageByKind: BuildKindShapeReports(definitions),
            SpecializationShapeUsage: SumShapeMetrics(specializations),
            SpecializationShapeUsageByKind: BuildKindShapeReports(
                specializations),
            BaseDefinitions: CountBy(
                definitions,
                definition => definition.BaseDefinition),
            Identity: BuildIdentityReport(definitions),
            SpecializationIdentity: BuildIdentityReport(specializations));
    }

    private static IReadOnlyList<KindShapeReport> BuildKindShapeReports(
        IEnumerable<DefinitionObservation> definitions)
    {
        return definitions
            .GroupBy(
                definition => Normalize(definition.Kind),
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new KindShapeReport(
                group.Key,
                SumShapeMetrics(group)))
            .ToArray();
    }

    private static IdentityReport BuildIdentityReport(
        IReadOnlyList<DefinitionObservation> definitions)
    {
        return new IdentityReport(
                UniqueTypeCount: CountUnique(
                    definitions,
                    definition => definition.Type),
                UniqueCanonicalCount: CountUnique(
                    definitions,
                    definition => definition.Canonical),
                UniqueSourceIdentityCount: CountUnique(
                    definitions,
                    definition => definition.SourceIdentity),
                MissingTypeSources: GetSources(
                    definitions,
                    definition => string.IsNullOrWhiteSpace(definition.Type)),
                MissingCanonicalSources: GetSources(
                    definitions,
                    definition => string.IsNullOrWhiteSpace(definition.Canonical)),
                MissingVersionSources: GetSources(
                    definitions,
                    definition => string.IsNullOrWhiteSpace(definition.Version)),
                MissingKindSources: GetSources(
                    definitions,
                    definition => string.IsNullOrWhiteSpace(definition.Kind)),
                DuplicateTypes: FindDuplicates(
                    definitions,
                    definition => definition.Type),
                DuplicateCanonicals: FindDuplicates(
                    definitions,
                    definition => definition.Canonical),
                DuplicateSourceIdentities: FindDuplicates(
                    definitions,
                    definition => definition.SourceIdentity));
    }

    internal static string Render(R5PackageReconnaissanceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var json = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

        return json
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n') +
            "\n";
    }

    private static PackageObservation ReadPackage(JsonElement root)
    {
        return new PackageObservation(
            GetOptionalString(root, "name"),
            GetOptionalString(root, "version"),
            GetOptionalString(root, "type"),
            GetOptionalString(root, "license"),
            GetStringArray(root, "fhirVersions"));
    }

    private static DefinitionObservation ReadDefinition(
        string sourceIdentity,
        JsonElement root)
    {
        JsonElement elementsProperty = default;
        var snapshotMissing =
            !root.TryGetProperty("snapshot", out var snapshot) ||
            snapshot.ValueKind != JsonValueKind.Object ||
            !snapshot.TryGetProperty("element", out elementsProperty) ||
            elementsProperty.ValueKind != JsonValueKind.Array;
        var elements = snapshotMissing
            ? []
            : elementsProperty.EnumerateArray().ToArray();
        var choiceElements = elements
            .Where(IsChoiceElement)
            .ToArray();

        return new DefinitionObservation(
            sourceIdentity,
            GetOptionalString(root, "type"),
            GetOptionalString(root, "url"),
            GetOptionalString(root, "version"),
            GetOptionalString(root, "kind"),
            GetOptionalString(root, "derivation"),
            GetOptionalBoolean(root, "abstract"),
            GetOptionalString(root, "baseDefinition"),
            snapshotMissing,
            new DefinitionShapeObservation(
                SnapshotElementCount: elements.Length,
                ChoiceElementCount: choiceElements.Length,
                ChoiceTypeAlternativeCount: choiceElements.Sum(
                    element => GetArrayLength(element, "type")),
                ContentReferenceElementCount: elements.Count(
                    element => !string.IsNullOrWhiteSpace(
                        GetOptionalString(element, "contentReference"))),
                SlicingElementCount: elements.Count(
                    element => HasNonNullProperty(element, "slicing")),
                ConstraintCount: elements.Sum(
                    element => GetArrayLength(element, "constraint")),
                BindingElementCount: elements.Count(
                    element => HasNonNullProperty(element, "binding")),
                FixedElementCount: elements.Count(
                    element => HasPropertyPrefix(element, "fixed")),
                PatternElementCount: elements.Count(
                    element => HasPropertyPrefix(element, "pattern"))));
    }

    private static ShapeMetrics SumShapeMetrics(
        IEnumerable<DefinitionObservation> source)
    {
        var definitions = source.ToArray();

        return new ShapeMetrics(
            DefinitionCount: definitions.Length,
            SnapshotElementCount: definitions.Sum(
                definition => definition.Shape.SnapshotElementCount),
            DefinitionsWithChoiceElements: definitions.Count(
                definition => definition.Shape.ChoiceElementCount > 0),
            ChoiceElementCount: definitions.Sum(
                definition => definition.Shape.ChoiceElementCount),
            ChoiceTypeAlternativeCount: definitions.Sum(
                definition => definition.Shape.ChoiceTypeAlternativeCount),
            DefinitionsWithContentReferences: definitions.Count(
                definition => definition.Shape.ContentReferenceElementCount > 0),
            ContentReferenceElementCount: definitions.Sum(
                definition => definition.Shape.ContentReferenceElementCount),
            DefinitionsWithSlicing: definitions.Count(
                definition => definition.Shape.SlicingElementCount > 0),
            SlicingElementCount: definitions.Sum(
                definition => definition.Shape.SlicingElementCount),
            DefinitionsWithConstraints: definitions.Count(
                definition => definition.Shape.ConstraintCount > 0),
            ConstraintCount: definitions.Sum(
                definition => definition.Shape.ConstraintCount),
            DefinitionsWithBindings: definitions.Count(
                definition => definition.Shape.BindingElementCount > 0),
            BindingElementCount: definitions.Sum(
                definition => definition.Shape.BindingElementCount),
            DefinitionsWithFixedValues: definitions.Count(
                definition => definition.Shape.FixedElementCount > 0),
            FixedElementCount: definitions.Sum(
                definition => definition.Shape.FixedElementCount),
            DefinitionsWithPatternValues: definitions.Count(
                definition => definition.Shape.PatternElementCount > 0),
            PatternElementCount: definitions.Sum(
                definition => definition.Shape.PatternElementCount));
    }

    private static IReadOnlyList<CountReport> CountBy(
        IEnumerable<DefinitionObservation> definitions,
        Func<DefinitionObservation, string?> selector)
    {
        return definitions
            .GroupBy(
                definition => Normalize(selector(definition)),
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CountReport(group.Key, group.Count()))
            .ToArray();
    }

    private static int CountUnique(
        IEnumerable<DefinitionObservation> definitions,
        Func<DefinitionObservation, string?> selector)
    {
        return definitions
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static IReadOnlyList<string> GetSources(
        IEnumerable<DefinitionObservation> definitions,
        Func<DefinitionObservation, bool> predicate)
    {
        return definitions
            .Where(predicate)
            .Select(definition => definition.SourceIdentity)
            .OrderBy(source => source, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<DuplicateIdentityReport> FindDuplicates(
        IEnumerable<DefinitionObservation> definitions,
        Func<DefinitionObservation, string?> selector)
    {
        return definitions
            .Where(definition => !string.IsNullOrWhiteSpace(selector(definition)))
            .GroupBy(definition => selector(definition)!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new DuplicateIdentityReport(
                group.Key,
                group.Count(),
                group
                    .Select(definition => definition.SourceIdentity)
                    .OrderBy(source => source, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }

    private static bool IsChoiceElement(JsonElement element)
    {
        return GetOptionalString(element, "path")?
            .EndsWith("[x]", StringComparison.Ordinal) is true;
    }

    private static bool HasPropertyPrefix(
        JsonElement element,
        string prefix)
    {
        return element
            .EnumerateObject()
            .Any(property =>
                property.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                property.Value.ValueKind is not JsonValueKind.Null and
                    not JsonValueKind.Undefined);
    }

    private static bool HasNonNullProperty(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is not JsonValueKind.Null and
                not JsonValueKind.Undefined;
    }

    private static int GetArrayLength(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Array
                ? property.GetArrayLength()
                : 0;
    }

    private static string? GetOptionalString(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static bool? GetOptionalBoolean(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static IReadOnlyList<string> GetStringArray(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? MissingValue
            : value;
    }
}

internal sealed record R5PackageReconnaissanceInput(
    PackageObservation Package,
    string ArchiveSha256,
    IReadOnlyList<DefinitionObservation> Definitions);

internal sealed record PackageObservation(
    string? PackageId,
    string? PackageVersion,
    string? PackageType,
    string? License,
    IReadOnlyList<string> FhirVersions);

internal sealed record DefinitionObservation(
    string SourceIdentity,
    string? Type,
    string? Canonical,
    string? Version,
    string? Kind,
    string? Derivation,
    bool? IsAbstract,
    string? BaseDefinition,
    bool IsSnapshotMissing,
    DefinitionShapeObservation Shape);

internal sealed record DefinitionShapeObservation(
    int SnapshotElementCount,
    int ChoiceElementCount,
    int ChoiceTypeAlternativeCount,
    int ContentReferenceElementCount,
    int SlicingElementCount,
    int ConstraintCount,
    int BindingElementCount,
    int FixedElementCount,
    int PatternElementCount);

internal sealed record R5PackageReconnaissanceReport(
    int SchemaVersion,
    PackageReport Package,
    DefinitionReport Definitions,
    ShapeMetrics ShapeUsage,
    IReadOnlyList<KindShapeReport> ShapeUsageByKind,
    ShapeMetrics SpecializationShapeUsage,
    IReadOnlyList<KindShapeReport> SpecializationShapeUsageByKind,
    IReadOnlyList<CountReport> BaseDefinitions,
    IdentityReport Identity,
    IdentityReport SpecializationIdentity);

internal sealed record PackageReport(
    string? PackageId,
    string? PackageVersion,
    string? PackageType,
    string? License,
    IReadOnlyList<string> FhirVersions,
    string ArchiveSha256);

internal sealed record DefinitionReport(
    int Total,
    IReadOnlyList<CountReport> ByKind,
    IReadOnlyList<CountReport> ByDerivation,
    IReadOnlyList<CountReport> ByKindAndDerivation,
    IReadOnlyList<CountReport> ByVersion,
    int AbstractCount,
    int ConcreteCount,
    int MissingAbstractFlagCount,
    IReadOnlyList<string> MissingDerivationSources,
    IReadOnlyList<string> MissingBaseDefinitionSources,
    IReadOnlyList<string> MissingSnapshotSources,
    IReadOnlyList<string> SpecializationMissingSnapshotSources);

internal sealed record ShapeMetrics(
    int DefinitionCount,
    int SnapshotElementCount,
    int DefinitionsWithChoiceElements,
    int ChoiceElementCount,
    int ChoiceTypeAlternativeCount,
    int DefinitionsWithContentReferences,
    int ContentReferenceElementCount,
    int DefinitionsWithSlicing,
    int SlicingElementCount,
    int DefinitionsWithConstraints,
    int ConstraintCount,
    int DefinitionsWithBindings,
    int BindingElementCount,
    int DefinitionsWithFixedValues,
    int FixedElementCount,
    int DefinitionsWithPatternValues,
    int PatternElementCount);

internal sealed record KindShapeReport(string Kind, ShapeMetrics Metrics);

internal sealed record CountReport(string Value, int Count);

internal sealed record IdentityReport(
    int UniqueTypeCount,
    int UniqueCanonicalCount,
    int UniqueSourceIdentityCount,
    IReadOnlyList<string> MissingTypeSources,
    IReadOnlyList<string> MissingCanonicalSources,
    IReadOnlyList<string> MissingVersionSources,
    IReadOnlyList<string> MissingKindSources,
    IReadOnlyList<DuplicateIdentityReport> DuplicateTypes,
    IReadOnlyList<DuplicateIdentityReport> DuplicateCanonicals,
    IReadOnlyList<DuplicateIdentityReport> DuplicateSourceIdentities);

internal sealed record DuplicateIdentityReport(
    string Value,
    int Count,
    IReadOnlyList<string> Sources);
