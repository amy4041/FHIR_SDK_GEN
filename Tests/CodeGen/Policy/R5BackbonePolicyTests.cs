using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.Core;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class R5BackbonePolicyTests
{
    private static readonly Lazy<IReadOnlyList<OfficialDefinition>> Definitions =
        new(ReadOfficialDefinitions);

    private readonly CSharpNameConverter _converter = new();

    [Fact]
    public void PublicPlacementAndNamingRulesAreExplicit()
    {
        using var policy = ReadPolicy("r5-backbone-policy.json");
        var root = policy.RootElement;
        var scope = root.GetProperty("scope");
        var shape = root.GetProperty("publicShape");
        var naming = root.GetProperty("naming");

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("5.0.0", root.GetProperty("fhirVersion").GetString());
        Assert.Equal("specialization", scope.GetProperty("definitionDerivation").GetString());
        Assert.Equal("resource", scope.GetProperty("definitionKind").GetString());
        Assert.Equal("BackboneElement", scope.GetProperty("elementTypeCode").GetString());
        Assert.Equal("public-top-level", shape.GetProperty("placement").GetString());
        Assert.Equal("MyFhirSdk.Resources", shape.GetProperty("namespace").GetString());
        Assert.Equal("Generated/R5/Resources", shape.GetProperty("outputDirectory").GetString());
        Assert.Equal("sealed", shape.GetProperty("classModifier").GetString());
        Assert.Equal(
            "MyFhirSdk.Core.BackboneElement",
            shape.GetProperty("baseClrType").GetString());
        Assert.True(shape.GetProperty("onePublicTopLevelTypePerFile").GetBoolean());
        Assert.Equal(
            "{CSharpTypeName}.g.cs",
            shape.GetProperty("fileNamePattern").GetString());
        Assert.Equal(
            "owner-fhir-type-via-csharp-name-converter",
            shape.GetProperty("resourceOwnerSource").GetString());
        Assert.Equal(
            "{ResourceOwner}",
            shape.GetProperty("resourceOwnerDirectoryPattern").GetString());
        Assert.Equal(
            "Generated/R5/Resources/{ResourceOwner}/{CSharpTypeName}.g.cs",
            shape.GetProperty("artifactPathPattern").GetString());
        Assert.Equal("complete-element-id", naming.GetProperty("identitySource").GetString());
        Assert.Equal(
            "owner-and-all-path-segments-concatenated",
            naming.GetProperty("segmentComposition").GetString());
        Assert.False(
            naming.GetProperty("automaticNumericSuffixesAllowed").GetBoolean());
    }

    [Fact]
    public void OfficialBackboneInventoryMatchesApprovedR5Shape()
    {
        var nodes = GetBackboneNodes();

        Assert.Equal(613, nodes.Count);
        Assert.Equal(141, nodes.Select(node => node.Owner).Distinct(StringComparer.Ordinal).Count());
        Assert.All(nodes, node => Assert.Equal("resource", node.OwnerKind));
        Assert.All(nodes, node => Assert.Equal("BackboneElement", node.BaseFhirType));
        Assert.Equal(384, nodes.Count(node => node.Depth == 1));
        Assert.Equal(170, nodes.Count(node => node.Depth == 2));
        Assert.Equal(47, nodes.Count(node => node.Depth == 3));
        Assert.Equal(12, nodes.Count(node => node.Depth == 4));
        Assert.Equal(
            nodes.Count,
            nodes.Select(node => node.ElementId).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(
            GetSelectedDefinitions()
                .SelectMany(definition => definition.Elements),
            element => element.TypeCodes.Contains(
                "BackboneType",
                StringComparer.Ordinal));
    }

    [Fact]
    public void FullPathNamesAndApprovedOverridesAreGloballyCollisionFree()
    {
        using var policy = ReadPolicy("r5-backbone-policy.json");
        var renames = GetExplicitRenames(policy);
        var backboneArtifacts = GetBackboneNodes()
            .Select(node => new
            {
                node.Owner,
                ClrName = GetBackboneName(node.ElementId, renames)
            })
            .ToArray();
        var resourceNames = GetSelectedDefinitions()
            .Where(definition => definition.Kind == "resource")
            .Select(definition => _converter.ConvertTypeName(definition.FhirType).Name!)
            .ToArray();
        var allNames = resourceNames
            .Concat(backboneArtifacts.Select(artifact => artifact.ClrName))
            .ToArray();
        var allClrIdentities = allNames
            .Select(name => $"MyFhirSdk.Resources.{name}")
            .ToArray();
        var allOutputPaths = resourceNames
            .Select(name => $"Generated/R5/Resources/{name}/{name}.g.cs")
            .Concat(backboneArtifacts.Select(artifact =>
                $"Generated/R5/Resources/{artifact.Owner}/{artifact.ClrName}.g.cs"))
            .ToArray();

        Assert.Equal(
            allClrIdentities.Length,
            allClrIdentities.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            allOutputPaths.Length,
            allOutputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            backboneArtifacts.Length,
            backboneArtifacts
                .Select(artifact => artifact.ClrName)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Contains(
            "Generated/R5/Resources/Patient/Patient.g.cs",
            allOutputPaths);
        Assert.Contains(
            "Generated/R5/Resources/Patient/PatientContact.g.cs",
            allOutputPaths);
        Assert.Contains(
            "Generated/R5/Resources/Claim/ClaimSubDetail.g.cs",
            allOutputPaths);
    }

    [Fact]
    public void CompatibilityOverridesAreCompleteAndPreserveCurrentPublicApi()
    {
        using var policy = ReadPolicy("r5-backbone-policy.json");
        var renames = GetExplicitRenames(policy);

        Assert.Equal(
            [
                "Claim.item.bodySite",
                "Claim.item.detail",
                "Claim.item.detail.subDetail"
            ],
            renames.Keys);
        Assert.Equal(
            ["ClaimBodySite", "ClaimDetail", "ClaimSubDetail"],
            renames.Values);
        var canonicalDifferences = GetBackboneNodes()
            .Where(node =>
                GetBackboneName(
                    node.ElementId,
                    new Dictionary<string, string>(StringComparer.Ordinal)) !=
                GetBackboneName(node.ElementId, renames))
            .Select(node => node.ElementId)
            .ToArray();
        Assert.Equal(renames.Keys, canonicalDifferences);

        var approvedNames = GetBackboneNodes()
            .Select(node => GetBackboneName(node.ElementId, renames))
            .ToHashSet(StringComparer.Ordinal);
        var currentBackbones = typeof(FhirObject).Assembly
            .GetExportedTypes()
            .Where(type =>
                type.Namespace == "MyFhirSdk.Resources" &&
                type.BaseType == typeof(BackboneElement))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(32, currentBackbones.Length);
        Assert.All(currentBackbones, type => Assert.Contains(type.Name, approvedNames));
        Assert.All(currentBackbones, type => Assert.True(type.IsSealed));
    }

    [Fact]
    public void BackboneNamingIsIndependentOfInventoryOrder()
    {
        using var policy = ReadPolicy("r5-backbone-policy.json");
        var renames = GetExplicitRenames(policy);
        var nodes = GetBackboneNodes();
        var original = CreateOrderedIdentitySnapshot(nodes, renames);
        var reversed = CreateOrderedIdentitySnapshot(nodes.Reverse(), renames);

        Assert.Equal(original, reversed);
    }

    [Fact]
    public void ContentReferenceAndCollisionBehaviorAreFailFast()
    {
        using var policy = ReadPolicy("r5-backbone-policy.json");
        var root = policy.RootElement;
        var references = root.GetProperty("referenceRules");
        var collisions = root.GetProperty("collisionRules");
        var unsupported = root.GetProperty("unsupportedShapeRules");

        Assert.False(
            references.GetProperty("contentReferenceCreatesDeclaration").GetBoolean());
        Assert.True(
            references
                .GetProperty("contentReferenceResolvesExistingElementIdentity")
                .GetBoolean());
        Assert.False(
            references.GetProperty("nestedBackboneInheritsContainingBackbone").GetBoolean());
        Assert.True(collisions.GetProperty("checkTopLevelResourceTypes").GetBoolean());
        Assert.True(collisions.GetProperty("checkAllBackboneTypes").GetBoolean());
        Assert.True(collisions.GetProperty("checkOutputPaths").GetBoolean());
        Assert.Equal(
            "fail-before-render",
            collisions.GetProperty("unapprovedCollisionDisposition").GetString());
        Assert.Equal(
            "fail-in-inventory",
            unsupported.GetProperty("unexpectedBackboneTypeNodeDisposition").GetString());
    }

    private string GetBackboneName(
        string elementId,
        IReadOnlyDictionary<string, string> renames)
    {
        if (renames.TryGetValue(elementId, out var approvedName))
        {
            return approvedName;
        }

        return string.Concat(
            elementId
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment =>
                {
                    var conversion = _converter.ConvertTypeName(segment);
                    Assert.True(
                        conversion.IsSuccess,
                        $"Backbone segment '{segment}' in '{elementId}' is invalid.");
                    return conversion.Name;
                }));
    }

    private string CreateOrderedIdentitySnapshot(
        IEnumerable<BackboneNode> nodes,
        IReadOnlyDictionary<string, string> renames)
    {
        return string.Join(
            '\n',
            nodes
                .Select(node =>
                    $"{node.ElementId}=MyFhirSdk.Resources." +
                    GetBackboneName(node.ElementId, renames))
                .OrderBy(identity => identity, StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> GetExplicitRenames(
        JsonDocument policy)
    {
        return policy.RootElement
            .GetProperty("explicitTypeRenames")
            .EnumerateArray()
            .OrderBy(
                rename => rename.GetProperty("elementId").GetString(),
                StringComparer.Ordinal)
            .ToDictionary(
                rename => rename.GetProperty("elementId").GetString()!,
                rename => rename.GetProperty("clrName").GetString()!,
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<BackboneNode> GetBackboneNodes()
    {
        return GetSelectedDefinitions()
            .SelectMany(definition => definition.Elements
                .Where(element => element.TypeCodes.Contains(
                    "BackboneElement",
                    StringComparer.Ordinal))
                .Select(element => new BackboneNode(
                    definition.FhirType,
                    definition.Kind,
                    element.ElementId,
                    "BackboneElement",
                    element.ElementId.Count(character => character == '.'))))
            .OrderBy(node => node.ElementId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<OfficialDefinition> GetSelectedDefinitions()
    {
        using var ownership = ReadPolicy("r5-model-ownership-policy.json");
        var externalTypes = ownership.RootElement
            .GetProperty("externalDefinitionNodes")
            .EnumerateArray()
            .Select(node => node.GetProperty("fhirType").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        return Definitions.Value
            .Where(definition =>
                definition.Derivation == "specialization" &&
                definition.Kind is "complex-type" or "resource" &&
                !externalTypes.Contains(definition.FhirType))
            .ToArray();
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
            definitions.Add(
                new OfficialDefinition(
                    fhirType,
                    kind,
                    derivation,
                    ReadElements(root)));
        }

        return definitions;
    }

    private static IReadOnlyList<OfficialElement> ReadElements(JsonElement definition)
    {
        if (!definition.TryGetProperty("snapshot", out var snapshot) ||
            !snapshot.TryGetProperty("element", out var elements))
        {
            return [];
        }

        return elements
            .EnumerateArray()
            .Select(element => new OfficialElement(
                element.GetProperty("id").GetString()!,
                element.TryGetProperty("type", out var types)
                    ? types
                        .EnumerateArray()
                        .Select(type => type.GetProperty("code").GetString()!)
                        .ToArray()
                    : []))
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
        IReadOnlyList<OfficialElement> Elements);

    private sealed record OfficialElement(
        string ElementId,
        IReadOnlyList<string> TypeCodes);

    private sealed record BackboneNode(
        string Owner,
        string OwnerKind,
        string ElementId,
        string BaseFhirType,
        int Depth);
}
