using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class R5ModelOwnershipPolicyTests
{
    private const string ExpectedDisposition = "external-handwritten";

    [Fact]
    public void AssemblyAndRuntimeContractOwnershipAreExplicit()
    {
        using var policy = ReadPolicy();
        var root = policy.RootElement;
        var layout = root.GetProperty("assemblyLayout");

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("5.0.0", root.GetProperty("fhirVersion").GetString());
        Assert.Equal("single-sdk-assembly", layout.GetProperty("phaseCMode").GetString());
        Assert.Equal("MyFhirSdk", layout.GetProperty("sdkAssemblyName").GetString());
        Assert.Equal(
            "MyFhirSdk",
            layout.GetProperty("generatedModelsAssemblyName").GetString());
        Assert.Equal(
            "sdk-project-reference",
            layout.GetProperty("codeGenRuntimeReference").GetString());
        Assert.Equal(
            "MyFhirSdk.CodeGen",
            layout.GetProperty("codeGenAssemblyName").GetString());
        Assert.True(layout.GetProperty("splitRequiresAdr").GetBoolean());

        var runtimeContracts = root
            .GetProperty("runtimeContracts")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(
            [
                "MyFhirSdk.Core.FhirObject",
                "MyFhirSdk.Core.IFhirExtensionValue",
                "MyFhirSdk.Core.PrimitiveType`1"
            ],
            runtimeContracts.Select(item => item.GetProperty("clrType").GetString()));
        Assert.All(
            runtimeContracts,
            item => Assert.Equal(
                "runtime-handwritten",
                item.GetProperty("declarationOwner").GetString()));
        Assert.All(
            runtimeContracts,
            item => Assert.NotNull(
                typeof(FhirObject).Assembly.GetType(
                    item.GetProperty("clrType").GetString()!)));

        var sdkAssembly = typeof(FhirObject).Assembly;
        Assert.Equal("MyFhirSdk", sdkAssembly.GetName().Name);
        Assert.Equal(
            "MyFhirSdk.CodeGen",
            typeof(FhirSdkGenerator).Assembly.GetName().Name);
        Assert.NotSame(sdkAssembly, typeof(FhirSdkGenerator).Assembly);
        Assert.Same(sdkAssembly, typeof(FhirString).Assembly);
        Assert.Same(sdkAssembly, typeof(HumanName).Assembly);
        Assert.Same(sdkAssembly, typeof(Patient).Assembly);
    }

    [Fact]
    public void BootstrapDefinitionsAreCompleteUniqueExternalNodes()
    {
        using var policy = ReadPolicy();
        var nodes = policy.RootElement
            .GetProperty("externalDefinitionNodes")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(10, nodes.Length);
        Assert.Equal(
            [
                "Base",
                "Element",
                "BackboneElement",
                "BackboneType",
                "DataType",
                "Resource",
                "DomainResource",
                "Extension",
                "Meta",
                "Narrative"
            ],
            nodes.Select(node => node.GetProperty("fhirType").GetString()));
        Assert.Equal(
            nodes.Length,
            nodes
                .Select(node => node.GetProperty("canonical").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            nodes.Length,
            nodes
                .Select(node => node.GetProperty("clrType").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            nodes,
            node => Assert.Equal(
                ExpectedDisposition,
                node.GetProperty("generationDisposition").GetString()));
        Assert.All(
            nodes,
            node => Assert.NotNull(
                typeof(FhirObject).Assembly.GetType(
                    node.GetProperty("clrType").GetString()!)));

        Assert.Equal(
            7,
            nodes.Count(node =>
                node.GetProperty("declarationOwner").GetString() ==
                "runtime-foundation-bootstrap"));
        Assert.Equal(
            3,
            nodes.Count(node =>
                node.GetProperty("declarationOwner").GetString() ==
                "r5-versioned-bootstrap"));
    }

    [Fact]
    public void BootstrapDefinitionIdentityMatchesOfficialPackage()
    {
        using var policy = ReadPolicy();
        var expectedByCanonical = policy.RootElement
            .GetProperty("externalDefinitionNodes")
            .EnumerateArray()
            .ToDictionary(
                node => node.GetProperty("canonical").GetString()!,
                node => node.Clone(),
                StringComparer.Ordinal);
        var actualByCanonical = ReadOfficialDefinitions(expectedByCanonical.Keys);

        Assert.Equal(expectedByCanonical.Count, actualByCanonical.Count);
        foreach (var (canonical, expected) in expectedByCanonical)
        {
            Assert.True(
                actualByCanonical.TryGetValue(canonical, out var actual),
                $"Official R5 package is missing bootstrap definition '{canonical}'.");
            Assert.Equal(
                expected.GetProperty("fhirType").GetString(),
                actual.GetProperty("type").GetString());
            Assert.Equal(
                expected.GetProperty("kind").GetString(),
                actual.GetProperty("kind").GetString());
            Assert.Equal(
                expected.GetProperty("abstract").GetBoolean(),
                actual.GetProperty("abstract").GetBoolean());
            Assert.Equal(
                GetNullableString(expected, "baseCanonical"),
                GetNullableString(actual, "baseDefinition"));
        }
    }

    [Fact]
    public void MigrationRulesPreventDuplicateBootstrapDeclarations()
    {
        using var policy = ReadPolicy();
        var migration = policy.RootElement.GetProperty("migrationPolicy");

        Assert.False(
            migration
                .GetProperty("phaseCMayGenerateExternalDefinitionDeclarations")
                .GetBoolean());
        Assert.True(
            migration.GetProperty("inventoryMustRetainExternalDefinitions").GetBoolean());
        Assert.True(
            migration.GetProperty("graphMustResolveExternalDefinitions").GetBoolean());
        Assert.True(
            migration.GetProperty("generatedModelsMayReferenceExternalDefinitions").GetBoolean());
        Assert.Equal(
            "Phase D",
            migration.GetProperty("codeGenRuntimeReferenceReassessmentPhase").GetString());
    }

    private static JsonDocument ReadPolicy()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "r5-model-ownership-policy.json");

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static Dictionary<string, JsonElement> ReadOfficialDefinitions(
        IEnumerable<string> selectedCanonicals)
    {
        var selected = selectedCanonicals.ToHashSet(StringComparer.Ordinal);
        var definitions = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
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
                !root.TryGetProperty("url", out var url) ||
                url.GetString() is not { } canonical ||
                !selected.Contains(canonical))
            {
                continue;
            }

            Assert.True(
                definitions.TryAdd(canonical, root.Clone()),
                $"Official R5 package contains duplicate bootstrap canonical '{canonical}'.");
        }

        return definitions;
    }

    private static string? GetNullableString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind != JsonValueKind.Null
                ? property.GetString()
                : null;
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
}
