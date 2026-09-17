using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Inventory;

public sealed class PackagePrimitiveSelectorTests
{
    private readonly PackagePrimitiveSelector _selector = new();

    [Fact]
    public async Task OfficialPackage_MatchesDirectoryInventoryIncludingUnsupportedXhtml()
    {
        var loaded = await new DefinitionPackageLoader().LoadAsync(
            new FileDefinitionPackageInput(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz")),
            new DefinitionPackageLoadOptions("hl7.fhir.r5.core", "5.0.0", "5.0.0"));
        Assert.True(loaded.IsSuccess);
        var package = Assert.IsType<LoadedDefinitionPackage>(loaded.Value);
        var result = _selector.Select(package, "5.0.0");
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var inventory = Assert.IsType<PrimitiveDefinitionInventory>(result.Value);
        Assert.Equal(21, inventory.Items.Count);
        Assert.Contains(inventory.Items, item => item.FhirTypeName == "xhtml");
        Assert.All(inventory.Items, item => Assert.StartsWith("package/", item.SourceFile));
        var directory = await new StructureDefinitionLoader().LoadAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "StructureDefinitions", "Primitives", "R5"),
            "5.0.0", StructureDefinitionLoadProfile.PrimitiveType);
        Assert.True(directory.IsSuccess);
        var expected = new PrimitiveDefinitionInventoryBuilder().Build(directory.Value, "5.0.0");
        Assert.True(expected.IsSuccess);
        Assert.Equal(expected.Value!.Items.Select(Describe), inventory.Items.Select(Describe));
        var reversed = _selector.Select(new LoadedDefinitionPackage(
            package.Identity, package.Definitions.Reverse()), "5.0.0");
        Assert.Equal(inventory.Items, reversed.Value!.Items);
    }

    [Theory]
    [InlineData("resourceType")]
    [InlineData("kind")]
    [InlineData("derivation")]
    [InlineData("id")]
    [InlineData("type")]
    [InlineData("url")]
    [InlineData("name")]
    [InlineData("baseDefinition")]
    [InlineData("version")]
    [InlineData("abstract")]
    [InlineData("snapshot")]
    [InlineData("differential")]
    public async Task MissingRequiredPrimitiveField_IsNotSilentlyFiltered(string field)
    {
        var broken = Primitive();
        broken.Remove(field);
        var loaded = await LoadArchiveAsync(
            ("package/valid.json", Primitive()), ("package/broken.json", broken));
        Assert.True(loaded.IsSuccess);
        var result = _selector.Select(loaded.Value!, "5.0.0");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.SourceFile == "package/broken.json");
        Assert.Equal(result.Diagnostics, _selector.Select(new LoadedDefinitionPackage(
            loaded.Value!.Identity, loaded.Value.Definitions.Reverse()), "5.0.0").Diagnostics);
    }

    [Theory]
    [InlineData("resourceType", "Patient")]
    [InlineData("kind", "complex-type")]
    [InlineData("derivation", "invalid")]
    [InlineData("version", "4.0.1")]
    [InlineData("fhirVersion", "4.0.1")]
    public async Task IncorrectPrimitiveClassificationOrVersion_Fails(string field, string value)
    {
        var broken = Primitive();
        broken[field] = value;
        var loaded = await LoadArchiveAsync(("package/broken.json", broken));
        Assert.True(loaded.IsSuccess);
        var result = _selector.Select(loaded.Value!, "5.0.0");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, item => item.SourceFile == "package/broken.json");
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("differential")]
    public void EmptyElementCollections_Fail(string field)
    {
        var broken = Primitive();
        broken[field] = new JsonObject { ["element"] = new JsonArray() };
        var result = _selector.Select(Package(("package/broken.json", broken)), "5.0.0");
        Assert.Contains(result.Diagnostics, item => item.Code == (field == "snapshot"
            ? GeneratorDiagnosticCodes.MissingSnapshot : GeneratorDiagnosticCodes.MissingDifferential));
    }

    [Fact]
    public void LegalComplexResourceAndProfiles_AreIgnored()
    {
        var complex = Primitive();
        complex["kind"] = "complex-type";
        complex["baseDefinition"] = "http://hl7.org/fhir/StructureDefinition/DataType";
        var resource = Primitive();
        resource["kind"] = "resource";
        resource["baseDefinition"] = "http://hl7.org/fhir/StructureDefinition/DomainResource";
        var profile = Primitive();
        profile["derivation"] = "constraint";
        profile["version"] = "custom-profile-version";
        profile["baseDefinition"] = "http://hl7.org/fhir/StructureDefinition/string";
        var result = _selector.Select(Package(
            ("package/primitive.json", Primitive()), ("package/complex.json", complex),
            ("package/resource.json", resource), ("package/profile.json", profile)), "5.0.0");
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("string", Assert.Single(result.Value!.Items).FhirTypeName);
        var empty = _selector.Select(Package(("package/profile.json", profile)), "5.0.0");
        Assert.False(empty.IsSuccess);
        Assert.Equal(GeneratorDiagnosticCodes.InvalidPrimitiveInventory, Assert.Single(empty.Diagnostics).Code);
    }

    [Theory]
    [InlineData("invalid-kind")]
    [InlineData("Primitive-Type")]
    public async Task XhtmlWithUnknownKind_IsRejectedInsteadOfIgnored(string kind)
    {
        var broken = Primitive("xhtml");
        broken["kind"] = kind;
        var forward = await LoadArchiveAsync(
            ("package/valid.json", Primitive()), ("package/broken.json", broken));
        var backward = await LoadArchiveAsync(
            ("package/broken.json", broken), ("package/valid.json", Primitive()));
        Assert.True(forward.IsSuccess);
        Assert.True(backward.IsSuccess);
        var result = _selector.Select(forward.Value!, "5.0.0");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == GeneratorDiagnosticCodes.InvalidPrimitiveInventory &&
            item.SourceFile == "package/broken.json" &&
            item.Message.Contains("kind", StringComparison.Ordinal));
        Assert.Equal(result.Diagnostics, _selector.Select(backward.Value!, "5.0.0").Diagnostics);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("url")]
    [InlineData("type")]
    [InlineData("baseDefinition")]
    public async Task PrimitiveConstraintWithMissingIdentity_IsRejected(string field)
    {
        foreach (var value in new string?[] { null, "", " " })
        {
            var broken = Primitive();
            broken["derivation"] = "constraint";
            broken["baseDefinition"] = "http://hl7.org/fhir/StructureDefinition/string";
            if (value is null) broken.Remove(field);
            else broken[field] = value;
            var forward = await LoadArchiveAsync(
                ("package/valid.json", Primitive("boolean")), ("package/profile.json", broken));
            var backward = await LoadArchiveAsync(
                ("package/profile.json", broken), ("package/valid.json", Primitive("boolean")));
            Assert.True(forward.IsSuccess);
            Assert.True(backward.IsSuccess);
            var result = _selector.Select(forward.Value!, "5.0.0");
            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(GeneratorDiagnosticCodes.InvalidPrimitiveInventory, diagnostic.Code);
            Assert.Equal("package/profile.json", diagnostic.SourceFile);
            Assert.Contains($"'{field}'", diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(result.Diagnostics, _selector.Select(backward.Value!, "5.0.0").Diagnostics);
        }
    }

    [Fact]
    public void PrimitiveConstraintWithoutSpecializationShape_IsStillIgnored()
    {
        var profile = Primitive();
        profile["derivation"] = "constraint";
        profile["baseDefinition"] = "http://hl7.org/fhir/StructureDefinition/string";
        profile["version"] = "profile-version";
        profile.Remove("snapshot");
        profile.Remove("differential");
        var result = _selector.Select(Package(
            ("package/valid.json", Primitive("boolean")), ("package/profile.json", profile)), "5.0.0");
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("boolean", Assert.Single(result.Value!.Items).FhirTypeName);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("url")]
    public async Task DuplicateIdentity_HasSameDiagnosticsForReversedArchiveEntries(string duplicateField)
    {
        var first = Primitive();
        var second = Primitive();
        second[duplicateField == "type" ? "url" : "type"] = "different";
        var forward = await LoadArchiveAsync(("package/a.json", first), ("package/z.json", second));
        var backward = await LoadArchiveAsync(("package/z.json", second), ("package/a.json", first));
        Assert.True(forward.IsSuccess);
        Assert.True(backward.IsSuccess);
        var result = _selector.Select(forward.Value!, "5.0.0");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry, diagnostic.Code);
        Assert.Equal("package/z.json", diagnostic.SourceFile);
        Assert.Equal(result.Diagnostics, _selector.Select(backward.Value!, "5.0.0").Diagnostics);
    }

    [Fact]
    public async Task ReversedArchiveEntries_ProduceIdenticalOrdinalInventory()
    {
        var forward = await LoadArchiveAsync(
            ("package/z.json", Primitive("string")), ("package/a.json", Primitive("boolean")));
        var backward = await LoadArchiveAsync(
            ("package/a.json", Primitive("boolean")), ("package/z.json", Primitive("string")));
        Assert.True(forward.IsSuccess);
        Assert.True(backward.IsSuccess);
        var first = _selector.Select(forward.Value!, "5.0.0");
        var second = _selector.Select(backward.Value!, "5.0.0");
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(new[] { "boolean", "string" }, first.Value!.Items.Select(item => item.FhirTypeName));
        Assert.Equal(first.Value.Items, second.Value!.Items);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    [Fact]
    public async Task MultipleMalformedEntries_ProduceIdenticalOrdinalDiagnostics()
    {
        var first = Primitive("boolean");
        first.Remove("resourceType");
        first.Remove("snapshot");
        var second = Primitive();
        second.Remove("kind");
        second.Remove("differential");
        var forward = await LoadArchiveAsync(("package/a.json", first), ("package/z.json", second));
        var backward = await LoadArchiveAsync(("package/z.json", second), ("package/a.json", first));
        Assert.True(forward.IsSuccess);
        Assert.True(backward.IsSuccess);
        var result = _selector.Select(forward.Value!, "5.0.0");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(result.Diagnostics, _selector.Select(backward.Value!, "5.0.0").Diagnostics);
        Assert.Equal(result.Diagnostics.OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.SourceFile, StringComparer.Ordinal)
            .ThenBy(item => item.DefinitionCanonical, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal), result.Diagnostics);
    }

    [Fact]
    public async Task NonStringPrimitiveResourceType_ReturnsReadDiagnostic()
    {
        var malformed = Primitive();
        malformed["resourceType"] = 42;
        var result = await LoadArchiveAsync(("package/broken.json", malformed));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == GeneratorDiagnosticCodes.DefinitionPackageReadFailure &&
            item.SourceFile == "package/broken.json");
    }

    [Fact]
    public void EmptyPackage_ReturnsNoPrimitiveDiagnostic()
    {
        var result = _selector.Select(Package(), "5.0.0");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(GeneratorDiagnosticCodes.InvalidPrimitiveInventory, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("4.0.1")]
    public void InvalidExpectedVersion_Fails(string expectedVersion)
    {
        var result = _selector.Select(Package(("package/string.json", Primitive())), expectedVersion);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }

    private static JsonObject Primitive(string type = "string") => JsonNode.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "StructureDefinitions", "Primitives", "R5",
        $"StructureDefinition-{type}.json")))!.AsObject();

    private static LoadedDefinitionPackage Package(params (string Name, JsonObject Json)[] entries) => new(
        new DefinitionPackageIdentity("hl7.fhir.r5.core", "5.0.0", "Core", "5.0.0"),
        entries.Select(entry => new LoadedStructureDefinition(entry.Name,
            entry.Json.Deserialize<StructureDefinitionDto>()!)));

    private static string Describe(PrimitiveDefinitionInventoryItem item) =>
        string.Join('|', item.FhirTypeName, item.Canonical, item.FhirVersion,
            item.BaseDefinition, item.DefinitionName, item.Description);

    private static async Task<GenerationResult<LoadedDefinitionPackage?>> LoadArchiveAsync(
        params (string Name, JsonObject Json)[] entries)
    {
        using var archive = new MemoryStream();
        using (var gzip = new GZipStream(archive, CompressionMode.Compress, leaveOpen: true))
        using (var writer = new TarWriter(gzip, leaveOpen: true))
        {
            void Add(string name, string json)
            {
                using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = content });
            }
            Add("package/package.json", """
                {"name":"hl7.fhir.r5.core","version":"5.0.0","type":"Core","fhirVersions":["5.0.0"]}
                """);
            foreach (var entry in entries) Add(entry.Name, entry.Json.ToJsonString());
        }
        return await new DefinitionPackageLoader().LoadAsync(new MemoryInput(archive.ToArray()),
            new DefinitionPackageLoadOptions("hl7.fhir.r5.core", "5.0.0", "5.0.0"));
    }

    private sealed class MemoryInput(byte[] bytes) : IDefinitionPackageInput
    {
        public string SourceIdentity => "fixture.tgz";
        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}
