using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Loading;

public sealed class DefinitionPackageLoaderTests
{
    private static readonly DefinitionPackageLoadOptions R5Options = new(
        "hl7.fhir.r5.core",
        "5.0.0",
        "5.0.0");

    private readonly DefinitionPackageLoader _loader = new();

    [Fact]
    public async Task LoadAsync_WithApprovedOfficialPackage_LoadsAllDefinitionsInOrdinalOrder()
    {
        var input = new FileDefinitionPackageInput(GetOfficialPackagePath());

        var result = await _loader.LoadAsync(input, R5Options);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        var package = Assert.IsType<LoadedDefinitionPackage>(result.Value);
        Assert.Equal("hl7.fhir.r5.core", package.Identity.PackageId);
        Assert.Equal("5.0.0", package.Identity.PackageVersion);
        Assert.Equal("Core", package.Identity.PackageType);
        Assert.Equal("5.0.0", package.Identity.FhirVersion);
        Assert.Equal(307, package.Definitions.Count);
        Assert.Equal(
            package.Definitions
                .Select(definition => definition.SourceFile)
                .OrderBy(source => source, StringComparer.Ordinal),
            package.Definitions.Select(definition => definition.SourceFile));
        Assert.All(
            package.Definitions,
            definition => Assert.StartsWith(
                "package/StructureDefinition-",
                definition.SourceFile,
                StringComparison.Ordinal));
        Assert.Throws<NotSupportedException>(
            () => ((IList<LoadedStructureDefinition>)package.Definitions).Clear());
    }

    [Theory]
    [InlineData("wrong.package", "5.0.0", "5.0.0", "Core")]
    [InlineData("hl7.fhir.r5.core", "5.0.1", "5.0.0", "Core")]
    [InlineData("hl7.fhir.r5.core", "5.0.0", "4.0.1", "Core")]
    [InlineData("hl7.fhir.r5.core", "5.0.0", "5.0.0", "IG")]
    public async Task LoadAsync_WithWrongExpectedIdentity_ReturnsFsg0027(
        string packageId,
        string packageVersion,
        string fhirVersion,
        string packageType)
    {
        var input = new FileDefinitionPackageInput(GetOfficialPackagePath());
        var options = new DefinitionPackageLoadOptions(
            packageId,
            packageVersion,
            fhirVersion,
            packageType);

        var result = await _loader.LoadAsync(input, options);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code ==
                GeneratorDiagnosticCodes.DefinitionPackageIdentityMismatch);
    }

    [Fact]
    public async Task LoadAsync_WithMalformedStructureDefinition_ReturnsSourceDiagnostic()
    {
        var input = CreateInput(
            ("package/package.json", CreatePackageJson()),
            ("package/StructureDefinition-Broken.json", "{"));

        var result = await _loader.LoadAsync(input, R5Options);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.DefinitionPackageReadFailure &&
                diagnostic.SourceFile == "package/StructureDefinition-Broken.json" &&
                diagnostic.Message.Contains(
                    "could not be deserialized",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_WithoutPackageDocument_ReturnsFsg0026()
    {
        var input = CreateInput(
            ("package/StructureDefinition-Patient.json", CreateDefinitionJson("Patient")));

        var result = await _loader.LoadAsync(input, R5Options);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.DefinitionPackageReadFailure &&
                diagnostic.Message.Contains("package/package.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_IsIndependentOfArchiveEntryOrder()
    {
        var entries = new[]
        {
            ("package/package.json", CreatePackageJson()),
            ("package/StructureDefinition-Patient.json", CreateDefinitionJson("Patient")),
            ("package/StructureDefinition-Address.json", CreateDefinitionJson("Address"))
        };
        var original = await _loader.LoadAsync(CreateInput(entries), R5Options);
        var reversed = await _loader.LoadAsync(
            CreateInput(entries.Reverse().ToArray()),
            R5Options);

        Assert.True(original.IsSuccess, Describe(original.Diagnostics));
        Assert.True(reversed.IsSuccess, Describe(reversed.Diagnostics));
        Assert.Equal(
            Assert.IsType<LoadedDefinitionPackage>(original.Value)
                .Definitions.Select(definition => definition.SourceFile),
            Assert.IsType<LoadedDefinitionPackage>(reversed.Value)
                .Definitions.Select(definition => definition.SourceFile));
    }

    [Fact]
    public async Task LoadAsync_DiscoversDefinitionByResourceTypeNotEntryFileName()
    {
        var input = CreateInput(
            ("package/package.json", CreatePackageJson()),
            ("package/unconventional-name.json", CreateDefinitionJson("Patient")),
            ("package/ordinary-resource.json", "{\"resourceType\":\"Patient\"}"));

        var result = await _loader.LoadAsync(input, R5Options);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var definition = Assert.Single(
            Assert.IsType<LoadedDefinitionPackage>(result.Value).Definitions);
        Assert.Equal("Patient", definition.Definition.Type);
        Assert.Equal("package/unconventional-name.json", definition.SourceFile);
    }

    private static IDefinitionPackageInput CreateInput(
        params (string Name, string Json)[] entries) =>
        new MemoryDefinitionPackageInput(CreateArchive(entries));

    private static byte[] CreateArchive(
        IReadOnlyList<(string Name, string Json)> entries)
    {
        using var archive = new MemoryStream();
        using (var gzip = new GZipStream(
                   archive,
                   CompressionLevel.SmallestSize,
                   leaveOpen: true))
        using (var writer = new TarWriter(gzip, leaveOpen: false))
        {
            foreach (var (name, json) in entries)
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(json))
                });
            }
        }

        return archive.ToArray();
    }

    private static string CreatePackageJson() =>
        """
        {
          "name": "hl7.fhir.r5.core",
          "version": "5.0.0",
          "type": "Core",
          "fhirVersions": ["5.0.0"]
        }
        """;

    private static string CreateDefinitionJson(string type) =>
        $$"""
        {
          "resourceType": "StructureDefinition",
          "id": "{{type}}",
          "url": "http://hl7.org/fhir/StructureDefinition/{{type}}",
          "version": "5.0.0",
          "fhirVersion": "5.0.0",
          "name": "{{type}}",
          "type": "{{type}}",
          "kind": "resource",
          "abstract": false,
          "baseDefinition": "http://hl7.org/fhir/StructureDefinition/DomainResource",
          "derivation": "specialization",
          "snapshot": { "element": [{ "id": "{{type}}", "path": "{{type}}" }] },
          "differential": { "element": [{ "id": "{{type}}", "path": "{{type}}" }] }
        }
        """;

    private static string GetOfficialPackagePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message));

    private sealed class MemoryDefinitionPackageInput(byte[] archiveBytes)
        : IDefinitionPackageInput
    {
        public string SourceIdentity => "memory://r5-package";

        public ValueTask<Stream> OpenReadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(archiveBytes));
        }
    }
}
