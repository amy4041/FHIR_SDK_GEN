using System.Text.Json;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Loading;

public sealed class StructureDefinitionLoaderTests
{
    private const string FhirVersion = "5.0.0";

    private readonly StructureDefinitionLoader _loader = new();

    [Fact]
    public async Task LoadAsync_WithOfficialHumanNameFixture_LoadsDefinition()
    {
        var fixturePath = GetFixturePath(
            "Valid",
            "StructureDefinition-HumanName.json");

        var result = await _loader.LoadAsync(fixturePath, FhirVersion);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);

        var loaded = Assert.Single(result.Value);
        Assert.Equal(Path.GetFullPath(fixturePath), loaded.SourceFile);
        Assert.Equal("HumanName", loaded.Definition.Id);
        Assert.Equal("5.0.0", loaded.Definition.Version);
        Assert.Equal(10, loaded.Definition.Snapshot?.Elements?.Count);
        Assert.Equal(8, loaded.Definition.Differential?.Elements?.Count);
    }

    [Fact]
    public async Task LoadAsync_WithDirectory_LoadsJsonFilesInOrdinalPathOrder()
    {
        using var directory = new TemporaryDirectory();
        await directory.WriteAsync(
            "zeta.json",
            CreateValidDefinitionJson("Zeta"));
        await directory.WriteAsync(
            "Alpha.json",
            CreateValidDefinitionJson("Alpha"));
        await directory.WriteAsync("ignored.txt", "not JSON");

        var result = await _loader.LoadAsync(directory.Path, FhirVersion);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["Alpha", "Zeta"],
            result.Value.Select(item => item.Definition.Id));
    }

    [Fact]
    public async Task LoadAsync_WithMalformedJson_ReturnsFsg0001()
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync("malformed.json", "{");

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.InvalidInput, diagnostic.Code);
        Assert.Equal(Path.GetFullPath(sourceFile), diagnostic.SourceFile);
    }

    [Fact]
    public async Task LoadAsync_WithNonStructureDefinition_ReturnsDiagnostic()
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync(
            "patient.json",
            """
            {
              "resourceType": "Patient",
              "id": "example"
            }
            """);

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.InvalidInput &&
                diagnostic.Message.Contains(
                    "resourceType",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_WithVersionMismatch_ReturnsFsg0002()
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync(
            "HumanName.json",
            CreateValidDefinitionJson("HumanName", version: "4.0.1"));

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == GeneratorDiagnosticCodes.FhirVersionMismatch);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/HumanName",
            diagnostic.DefinitionCanonical);
        Assert.Equal("4.0.1", diagnostic.DefinitionVersion);
    }

    [Fact]
    public async Task LoadAsync_WithoutSnapshot_ReturnsFsg0003()
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync(
            "HumanName.json",
            CreateValidDefinitionJson("HumanName", includeSnapshot: false));

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.MissingSnapshot);
    }

    [Fact]
    public async Task LoadAsync_WithoutDifferential_ReturnsFsg0004()
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync(
            "HumanName.json",
            CreateValidDefinitionJson("HumanName", includeDifferential: false));

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.MissingDifferential);
    }

    [Theory]
    [InlineData("resource", "specialization")]
    [InlineData("complex-type", "constraint")]
    public async Task LoadAsync_WithUnsupportedDefinition_ReturnsFsg0005(
        string kind,
        string derivation)
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync(
            "Unsupported.json",
            CreateValidDefinitionJson(
                "Unsupported",
                kind: kind,
                derivation: derivation));

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedDefinition);
    }

    [Fact]
    public async Task LoadAsync_WithMissingRequiredField_ReturnsDiagnostic()
    {
        using var directory = new TemporaryDirectory();
        var sourceFile = await directory.WriteAsync(
            "missing-name.json",
            CreateValidDefinitionJson("HumanName", includeName: false));

        var result = await _loader.LoadAsync(sourceFile, FhirVersion);

        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.InvalidInput &&
                diagnostic.Message.Contains("'name'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_WithMissingPath_ReturnsFsg0001()
    {
        using var directory = new TemporaryDirectory();
        var missingPath = Path.Combine(directory.Path, "missing.json");

        var result = await _loader.LoadAsync(missingPath, FhirVersion);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.InvalidInput, diagnostic.Code);
        Assert.Equal(Path.GetFullPath(missingPath), diagnostic.SourceFile);
    }

    [Fact]
    public async Task LoadAsync_WithOneInvalidFile_ContinuesLoadingOtherFiles()
    {
        using var directory = new TemporaryDirectory();
        await directory.WriteAsync(
            "Address.json",
            CreateValidDefinitionJson("Address"));
        await directory.WriteAsync("Broken.json", "{");

        var result = await _loader.LoadAsync(directory.Path, FhirVersion);

        Assert.False(result.IsSuccess);
        var loaded = Assert.Single(result.Value);
        Assert.Equal("Address", loaded.Definition.Id);
        Assert.Single(result.Diagnostics);
    }

    private static string GetFixturePath(params string[] segments)
    {
        return segments.Aggregate(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "StructureDefinitions"),
            Path.Combine);
    }

    private static string CreateValidDefinitionJson(
        string id,
        string version = FhirVersion,
        string kind = "complex-type",
        string derivation = "specialization",
        bool includeSnapshot = true,
        bool includeDifferential = true,
        bool includeName = true)
    {
        var definition = new Dictionary<string, object?>
        {
            ["resourceType"] = "StructureDefinition",
            ["id"] = id,
            ["url"] = $"http://hl7.org/fhir/StructureDefinition/{id}",
            ["version"] = version,
            ["type"] = id,
            ["kind"] = kind,
            ["abstract"] = false,
            ["baseDefinition"] = "http://hl7.org/fhir/StructureDefinition/DataType",
            ["derivation"] = derivation
        };

        if (includeName)
        {
            definition["name"] = id;
        }

        if (includeSnapshot)
        {
            definition["snapshot"] = CreateElementContainer(id);
        }

        if (includeDifferential)
        {
            definition["differential"] = CreateElementContainer(id);
        }

        return JsonSerializer.Serialize(definition);
    }

    private static object CreateElementContainer(string id)
    {
        return new
        {
            element = new[]
            {
                new
                {
                    id,
                    path = id
                }
            }
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MyFhirSdk.CodeGen.Tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public async Task<string> WriteAsync(string fileName, string content)
        {
            var filePath = System.IO.Path.Combine(Path, fileName);
            await File.WriteAllTextAsync(filePath, content);
            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
