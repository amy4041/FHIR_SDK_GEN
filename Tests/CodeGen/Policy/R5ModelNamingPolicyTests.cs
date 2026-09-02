using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.Types;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class R5ModelNamingPolicyTests
{
    private static readonly Lazy<IReadOnlyList<OfficialDefinition>> Definitions =
        new(ReadOfficialDefinitions);

    private readonly CSharpNameConverter _converter = new();

    [Fact]
    public void NamespaceAndFileRulesAreExplicit()
    {
        using var policy = ReadNamingPolicy();
        var root = policy.RootElement;
        var comparers = root.GetProperty("comparers");

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("5.0.0", root.GetProperty("fhirVersion").GetString());
        Assert.Equal("ordinal", comparers.GetProperty("symbolIdentity").GetString());
        Assert.Equal(
            "ordinal-ignore-case",
            comparers.GetProperty("outputPathIdentity").GetString());
        Assert.Equal(
            "ordinal",
            comparers.GetProperty("deterministicOrdering").GetString());

        var rules = root
            .GetProperty("namespaceRules")
            .EnumerateArray()
            .ToDictionary(
                rule => rule.GetProperty("category").GetString()!,
                rule => rule,
                StringComparer.Ordinal);
        Assert.Equal(6, rules.Count);
        AssertNamespaceRule(
            rules["primitive-wrapper"],
            "MyFhirSdk.Primitives",
            "Generated/R5/Primitives");
        AssertNamespaceRule(
            rules["complex-datatype"],
            "MyFhirSdk.Types",
            "Generated/R5/Types");
        AssertNamespaceRule(
            rules["resource"],
            "MyFhirSdk.Resources",
            "Generated/R5/Resources");
        AssertNamespaceRule(
            rules["model-metadata"],
            "MyFhirSdk.ModelMetadata.R5",
            "Generated/R5/ModelMetadata");
        AssertNamespaceRule(
            rules["backbone"],
            "MyFhirSdk.Resources",
            "Generated/R5/Resources");
        Assert.Equal(
            "from-r5-model-ownership-policy",
            rules["external-definition"].GetProperty("namespace").GetString());

        var fileNaming = root.GetProperty("fileNaming");
        Assert.Equal(
            "{CSharpTypeName}.g.cs",
            fileNaming.GetProperty("modelFilePattern").GetString());
        Assert.True(
            fileNaming
                .GetProperty("onePublicTopLevelTypePerModelFile")
                .GetBoolean());
        Assert.Equal(
            "model-generation-manifest.json",
            fileNaming.GetProperty("manifestFileName").GetString());
        Assert.Equal(
            "windows-linux",
            fileNaming.GetProperty("portableFileNamePolicy").GetString());
        Assert.True(
            fileNaming.GetProperty("rejectRootedOrTraversalSegments").GetBoolean());
        Assert.True(fileNaming.GetProperty("rejectWindowsDeviceNames").GetBoolean());
    }

    [Fact]
    public void OfficialTopLevelModelNamesAndPathsAreCollisionFree()
    {
        using var namingPolicy = ReadNamingPolicy();
        using var ownershipPolicy = ReadOwnershipPolicy();
        var externalTypes = GetExternalFhirTypes(ownershipPolicy);
        var candidates = GetGenerationCandidates(externalTypes);
        var categoryRules = namingPolicy.RootElement
            .GetProperty("namespaceRules")
            .EnumerateArray()
            .Where(rule =>
                rule.GetProperty("category").GetString() is
                    "complex-datatype" or "resource")
            .ToDictionary(
                rule => rule.GetProperty("category").GetString()!,
                rule => (
                    Namespace: rule.GetProperty("namespace").GetString()!,
                    OutputDirectory:
                        rule.GetProperty("outputDirectory").GetString()!),
                StringComparer.Ordinal);
        var identities = new List<string>();
        var paths = new List<string>();

        Assert.Equal(199, candidates.Count);
        Assert.Equal(39, candidates.Count(item => item.Kind == "complex-type"));
        Assert.Equal(160, candidates.Count(item => item.Kind == "resource"));

        foreach (var candidate in candidates)
        {
            var conversion = _converter.ConvertTypeName(candidate.FhirType);
            Assert.True(
                conversion.IsSuccess,
                $"FHIR type '{candidate.FhirType}' is not a valid C# type identity.");
            var category = candidate.Kind == "resource"
                ? "resource"
                : "complex-datatype";
            var rule = categoryRules[category];
            var cSharpName = conversion.Name!;

            identities.Add($"{rule.Namespace}.{cSharpName}");
            paths.Add($"{rule.OutputDirectory}/{cSharpName}.g.cs");
        }

        Assert.Equal(
            identities.Count,
            identities.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            paths.Count,
            paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            paths,
            path =>
            {
                Assert.EndsWith(".g.cs", path, StringComparison.Ordinal);
                Assert.DoesNotContain('\\', path);
                Assert.DoesNotContain("../", path, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void OfficialSelfNameCollisionsMatchExplicitRenames()
    {
        using var namingPolicy = ReadNamingPolicy();
        using var ownershipPolicy = ReadOwnershipPolicy();
        var externalTypes = GetExternalFhirTypes(ownershipPolicy);
        var candidates = GetGenerationCandidates(externalTypes);
        var actualCollisions = candidates
            .SelectMany(definition =>
            {
                var typeName = _converter.ConvertTypeName(definition.FhirType).Name;
                return definition.DirectNonChoiceElementIds
                    .Where(elementId =>
                        _converter.ConvertPropertyName(elementId).Name == typeName);
            })
            .OrderBy(elementId => elementId, StringComparer.Ordinal)
            .ToArray();
        var renames = namingPolicy.RootElement
            .GetProperty("explicitMemberRenames")
            .EnumerateArray()
            .OrderBy(
                rename => rename.GetProperty("elementId").GetString(),
                StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["Expression.expression", "Reference.reference"],
            actualCollisions);
        Assert.Equal(actualCollisions, renames.Select(GetElementId));
        Assert.Equal(
            ["ExpressionValue", "ReferenceValue"],
            renames.Select(rename => rename.GetProperty("clrName").GetString()));
        Assert.Equal(
            ["expression", "reference"],
            renames.Select(rename => rename.GetProperty("jsonName").GetString()));

        var referenceProperty = typeof(Reference).GetProperty("ReferenceValue");
        Assert.NotNull(referenceProperty);
        Assert.Equal(
            "reference",
            referenceProperty!
                .GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                .Cast<JsonPropertyNameAttribute>()
                .Single()
                .Name);
    }

    [Fact]
    public void ApprovedRenamesLeaveNoDirectMemberOrReservedNameCollisions()
    {
        using var namingPolicy = ReadNamingPolicy();
        using var ownershipPolicy = ReadOwnershipPolicy();
        var root = namingPolicy.RootElement;
        var externalTypes = GetExternalFhirTypes(ownershipPolicy);
        var renames = root
            .GetProperty("explicitMemberRenames")
            .EnumerateArray()
            .ToDictionary(
                GetElementId,
                rename => rename.GetProperty("clrName").GetString()!,
                StringComparer.Ordinal);
        var syntheticResourceNames = root
            .GetProperty("syntheticMembers")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("category").GetString() == "concrete-resource")
            .Select(item => item.GetProperty("clrName").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(["ResourceType"], syntheticResourceNames);

        foreach (var definition in GetGenerationCandidates(externalTypes))
        {
            var typeName = _converter.ConvertTypeName(definition.FhirType).Name!;
            var memberNames = definition.DirectNonChoiceElementIds
                .Select(elementId =>
                    renames.TryGetValue(elementId, out var approvedName)
                        ? approvedName
                        : _converter.ConvertPropertyName(elementId).Name!)
                .ToArray();

            Assert.DoesNotContain(typeName, memberNames);
            Assert.Equal(
                memberNames.Length,
                memberNames.Distinct(StringComparer.Ordinal).Count());
            if (definition.Kind == "resource")
            {
                Assert.DoesNotContain(
                    memberNames,
                    memberName => syntheticResourceNames.Contains(memberName));
            }
        }
    }

    [Fact]
    public void CollisionHandlingIsFailFastAndDeferredScopesStayDeferred()
    {
        using var policy = ReadNamingPolicy();
        var root = policy.RootElement;
        var identifiers = root.GetProperty("identifierRules");
        var collisions = root.GetProperty("collisionRules");
        var deferred = root.GetProperty("deferredDecisions");

        Assert.False(
            identifiers.GetProperty("automaticNumericSuffixesAllowed").GetBoolean());
        Assert.True(collisions.GetProperty("checkFullyQualifiedTypeIdentity").GetBoolean());
        Assert.True(collisions.GetProperty("checkOutputPathIdentity").GetBoolean());
        Assert.True(collisions.GetProperty("checkDeclaringTypeName").GetBoolean());
        Assert.True(collisions.GetProperty("checkInheritedMembers").GetBoolean());
        Assert.True(collisions.GetProperty("checkSyntheticMembers").GetBoolean());
        Assert.Equal(
            "fail-before-render",
            collisions.GetProperty("unapprovedCollisionDisposition").GetString());
        Assert.Equal(
            "C0-006",
            deferred.GetProperty("choiceMemberNamingAndRepresentation").GetString());
        Assert.Equal(
            "C0-006",
            deferred.GetProperty("openTypeNamingAndRepresentation").GetString());
    }

    private static void AssertNamespaceRule(
        JsonElement rule,
        string expectedNamespace,
        string expectedOutputDirectory)
    {
        Assert.Equal(expectedNamespace, rule.GetProperty("namespace").GetString());
        Assert.Equal(
            expectedOutputDirectory,
            rule.GetProperty("outputDirectory").GetString());
    }

    private static string GetElementId(JsonElement rename)
    {
        return rename.GetProperty("elementId").GetString()!;
    }

    private static IReadOnlySet<string> GetExternalFhirTypes(JsonDocument policy)
    {
        return policy.RootElement
            .GetProperty("externalDefinitionNodes")
            .EnumerateArray()
            .Select(node => node.GetProperty("fhirType").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<OfficialDefinition> GetGenerationCandidates(
        IReadOnlySet<string> externalTypes)
    {
        return Definitions.Value
            .Where(definition =>
                definition.Derivation == "specialization" &&
                definition.Kind is "complex-type" or "resource" &&
                !externalTypes.Contains(definition.FhirType))
            .OrderBy(definition => definition.FhirType, StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonDocument ReadNamingPolicy()
    {
        return ReadPolicy("r5-model-naming-policy.json");
    }

    private static JsonDocument ReadOwnershipPolicy()
    {
        return ReadPolicy("r5-model-ownership-policy.json");
    }

    private static JsonDocument ReadPolicy(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Policy", fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static IReadOnlyList<OfficialDefinition> ReadOfficialDefinitions()
    {
        var definitions = new List<OfficialDefinition>();
        using var archive = File.OpenRead(GetArchivePath());
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (entry.DataStream is null ||
                !entry.Name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(entry.DataStream);
            var root = document.RootElement;
            if (!root.TryGetProperty("resourceType", out var resourceType) ||
                resourceType.GetString() != "StructureDefinition" ||
                !root.TryGetProperty("type", out var typeProperty) ||
                typeProperty.GetString() is not { } fhirType ||
                !root.TryGetProperty("kind", out var kindProperty) ||
                kindProperty.GetString() is not { } kind)
            {
                continue;
            }

            var derivation = root.TryGetProperty("derivation", out var derivationProperty)
                ? derivationProperty.GetString()
                : null;
            var directElements = ReadDirectNonChoiceElementIds(root, fhirType);
            definitions.Add(
                new OfficialDefinition(
                    fhirType,
                    kind,
                    derivation,
                    directElements));
        }

        return definitions;
    }

    private static IReadOnlyList<string> ReadDirectNonChoiceElementIds(
        JsonElement definition,
        string fhirType)
    {
        if (!definition.TryGetProperty("differential", out var differential) ||
            !differential.TryGetProperty("element", out var elements))
        {
            return [];
        }

        var prefix = $"{fhirType}.";
        return elements
            .EnumerateArray()
            .Select(element => element.GetProperty("id").GetString())
            .Where(elementId =>
                elementId is not null &&
                elementId.StartsWith(prefix, StringComparison.Ordinal) &&
                !elementId[prefix.Length..].Contains('.', StringComparison.Ordinal) &&
                !elementId.EndsWith("[x]", StringComparison.Ordinal))
            .Select(elementId => elementId!)
            .OrderBy(elementId => elementId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetArchivePath()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");

        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(
                "The approved official R5 core package fixture was not copied.",
                path);
    }

    private sealed record OfficialDefinition(
        string FhirType,
        string Kind,
        string? Derivation,
        IReadOnlyList<string> DirectNonChoiceElementIds);
}
