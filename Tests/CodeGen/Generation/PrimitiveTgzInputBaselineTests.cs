using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Compatibility;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class PrimitiveTgzInputBaselineTests : IDisposable
{
    private const string ManifestName = "primitive-generation-manifest.json";
    private const string RegistryName = "PrimitiveRegistry.Composition.g.cs";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "MyFhirSdk-PrimitiveTgzBaseline", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RebuildAndVerifyEntryBaseline()
    {
        Directory.CreateDirectory(_root);
        var packagePrimitives = await ReadApprovedPackagePrimitivesAsync();
        var directory = Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "StructureDefinitions", "Primitives", "R5");
        var fixtures = ReadFiles(directory, "*.json");
        Assert.Equal(21, packagePrimitives.Count);
        Assert.Equal(fixtures.Keys, packagePrimitives.Keys);
        foreach (var (name, bytes) in packagePrimitives)
        {
            // Compare the entire JSON, including fields not represented by CodeGen DTOs.
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fixtures[name]), JsonNode.Parse(bytes)),
                $"The directory fixture differs from the package primitive: {name}");
        }

        // Test-only materialization into names approved by the pinned fixture set.
        // This is not production archive extraction or primitive .tgz CLI support.
        var materialized = Path.Combine(_root, "package-primitives");
        Directory.CreateDirectory(materialized);
        foreach (var (name, bytes) in packagePrimitives.Reverse())
        {
            Assert.Equal(name, Path.GetFileName(name));
            await File.WriteAllBytesAsync(Path.Combine(materialized, name), bytes);
        }

        var directoryOutput = await GenerateAsync(directory, "directory-output");
        var packageOutput = await GenerateAsync(materialized, "package-output");
        var repeatedOutput = await GenerateAsync(directory, "repeated-output");
        var archiveOutput = await GenerateAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz"), "archive-output");
        AssertArtifactsEqual(directoryOutput, archiveOutput);
        AssertArtifactsEqual(directoryOutput, packageOutput);
        AssertArtifactsEqual(directoryOutput, repeatedOutput);
        AssertArtifactsEqual(directoryOutput, ReadFiles(Path.Combine(
            AppContext.BaseDirectory, "CommittedGenerated", "R5", "Primitives")));

        using var manifest = JsonDocument.Parse(directoryOutput[ManifestName]);
        var root = manifest.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        var version = GenerationCompatibilityMatrix.CodeGenVersion;
        Assert.Equal(version, root.GetProperty("codeGenVersion").GetString());
        var decisions = root.GetProperty("primitives").EnumerateArray().ToArray();
        var supported = decisions.Count(item => item.GetProperty("supportStatus").GetString() == "supported");
        Assert.Equal(20, supported);
        Assert.Equal(packagePrimitives.Count, decisions.Length);
        Assert.Equal("xhtml", Assert.Single(decisions, item =>
            item.GetProperty("supportStatus").GetString() == "unsupported")
            .GetProperty("fhirTypeName").GetString());
        Assert.Equal(supported, directoryOutput.Keys.Count(name => name != RegistryName && name.EndsWith(".g.cs", StringComparison.Ordinal)));
        Assert.Contains(RegistryName, directoryOutput.Keys);
        Assert.Equal(supported + 2, directoryOutput.Count);
        var artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
        Assert.Equal(supported + 1, artifacts.Length);
        Assert.Equal(directoryOutput.Keys.Where(name => name != ManifestName),
            artifacts.Select(item => item.GetProperty("fileName").GetString()));
        foreach (var artifact in artifacts)
        {
            Assert.Equal(artifact.GetProperty("sha256").GetString(),
                Hash(directoryOutput[artifact.GetProperty("fileName").GetString()!]));
        }

        var evidence = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [$"cli-help-{version}.txt"] = await CaptureHelpAsync(),
            [$"directory-artifact-inventory-{version}.txt"] = Utf8(string.Join("\n", directoryOutput.Keys) + "\n"),
            [$"directory-artifact-hashes-{version}.txt"] = Utf8(string.Join("\n", directoryOutput.Select(
                item => $"{Hash(item.Value)}  {item.Key}")) + "\n"),
            [$"directory-manifest-{version}.json"] = directoryOutput[ManifestName],
            ["package-primitive-fixture-contract.txt"] = BuildFixtureContract(packagePrimitives)
        };
        var exportPath = Environment.GetEnvironmentVariable("MYFHIRSDK_PRIMITIVE_P0_EXPORT");
        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            // Export evidence only. Never overwrite the approved snapshots automatically.
            Directory.CreateDirectory(exportPath);
            foreach (var (name, bytes) in evidence)
            {
                await File.WriteAllBytesAsync(Path.Combine(exportPath, name), bytes);
            }
        }

        var baseline = Path.Combine(AppContext.BaseDirectory, "Baselines", "PrimitiveTgzInput");
        // Historical 1.0.0 evidence remains immutable. Only tool/CodeGen and descriptor
        // provenance may change; primitive decisions, source hashes and Runtime bytes may not.
        var historical = JsonNode.Parse(File.ReadAllBytes(Path.Combine(baseline, "directory-manifest-1.0.0.json")))!;
        historical["codeGenVersion"] = version;
        historical["compatibility"]!["codeGenVersion"] = version;
        historical["compatibility"]!["tool"]!["version"] = GenerationCompatibilityMatrix.ToolVersion;
        historical["compatibility"]!["runtimeDescriptor"]!["sha256"] = CodeGenTestRuntime.RuntimeContract.DescriptorSha256;
        Assert.True(JsonNode.DeepEquals(historical, JsonNode.Parse(directoryOutput[ManifestName])),
            "Only approved version/descriptor provenance may differ from the P0 manifest.");
        foreach (var line in File.ReadAllLines(Path.Combine(baseline, "directory-artifact-hashes-1.0.0.txt")))
        {
            var name = line[66..];
            if (name != ManifestName) Assert.Equal(line[..64], Hash(directoryOutput[name]));
        }
        foreach (var (name, bytes) in evidence)
        {
            Assert.True(File.Exists(Path.Combine(baseline, name)), $"Missing approved P0 baseline: {name}");
            Assert.Equal(File.ReadAllBytes(Path.Combine(baseline, name)), bytes);
        }
    }

    private static async Task<SortedDictionary<string, byte[]>> ReadApprovedPackagePrimitivesAsync()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz");
        using var packageLock = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Policy", "r5-package-lock.json")));
        var contract = packageLock.RootElement;
        await using (var archive = File.OpenRead(archivePath))
        {
            Assert.Equal(contract.GetProperty("sha256").GetString(),
                Convert.ToHexString(await SHA256.HashDataAsync(archive)).ToLowerInvariant());
        }
        var loaded = await new DefinitionPackageLoader().LoadAsync(
            new FileDefinitionPackageInput(archivePath), new DefinitionPackageLoadOptions(
                contract.GetProperty("packageId").GetString()!,
                contract.GetProperty("packageVersion").GetString()!,
                Assert.Single(contract.GetProperty("fhirVersions").EnumerateArray()).GetString()!,
                contract.GetProperty("packageType").GetString()!));
        Assert.True(loaded.IsSuccess, string.Join("\n", loaded.Diagnostics));
        var package = Assert.IsType<LoadedDefinitionPackage>(loaded.Value);
        // A fixture oracle for the immutable official archive, not the P2 selector.
        var selected = package.Definitions.Where(item =>
            item.Definition.ResourceType == "StructureDefinition" &&
            item.Definition.Kind == "primitive-type" &&
            item.Definition.Derivation == "specialization" &&
            item.Definition.Version == "5.0.0").ToArray();
        Assert.Equal(selected.Length, selected.Select(item => item.Definition.Type).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(selected.Length, selected.Select(item => item.Definition.Url).Distinct(StringComparer.Ordinal).Count());
        var names = selected.Select(item => item.SourceFile).ToHashSet(StringComparer.Ordinal);
        var result = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        // Read raw bytes only after the production loader validated the pinned package.
        // No archive-controlled path is written to the filesystem.
        using var input = File.OpenRead(archivePath);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) is not null)
        {
            if (!names.Contains(entry.Name)) continue;
            Assert.NotNull(entry.DataStream);
            using var content = new MemoryStream();
            await entry.DataStream.CopyToAsync(content);
            result.Add(Path.GetFileName(entry.Name), content.ToArray());
        }
        Assert.Equal(selected.Length, result.Count);
        return result;
    }

    private async Task<SortedDictionary<string, byte[]>> GenerateAsync(string definitions, string outputName)
    {
        var output = Path.Combine(_root, outputName);
        var result = await CodeGenTestRuntime.CreatePrimitivePipeline().GenerateAsync(new PrimitiveGenerationOptions(
            definitions, Path.Combine(AppContext.BaseDirectory, "Policy", "primitive-generation-policy.json"),
            output, "5.0.0", "hl7.fhir.r5.core", "5.0.0", PrimitiveGenerationPipeline.DefaultCodeGenVersion));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        return ReadFiles(output);
    }

    private static async Task<byte[]> CaptureHelpAsync()
    {
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };
        var cli = new GeneratorCli(output, error,
            new GeneratorCommandLineParser(new ToolAssetResolver(AppContext.BaseDirectory)));
        Assert.Equal(0, await cli.RunAsync(["--help"]));
        Assert.Equal(string.Empty, error.ToString());
        return Utf8(output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static byte[] BuildFixtureContract(SortedDictionary<string, byte[]> primitives)
    {
        using var packageLock = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Policy", "r5-package-lock.json")));
        var contract = packageLock.RootElement;
        return Utf8($"package: {contract.GetProperty("packageId").GetString()}#{contract.GetProperty("packageVersion").GetString()}\n" +
            $"archive-sha256: {contract.GetProperty("sha256").GetString()}\n" +
            "selection: resourceType=StructureDefinition; kind=primitive-type; derivation=specialization; version=5.0.0\n" +
            $"primitive-count: {primitives.Count}\n" +
            "entry-sha256 (exact archive entry bytes):\n" +
            string.Join("\n", primitives.Select(item => $"{Hash(item.Value)}  package/{item.Key}")) + "\n");
    }

    private static SortedDictionary<string, byte[]> ReadFiles(string directory, string pattern = "*") =>
        new(Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes, StringComparer.Ordinal), StringComparer.Ordinal);

    private static void AssertArtifactsEqual(
        SortedDictionary<string, byte[]> expected, SortedDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var name in expected.Keys) Assert.Equal(expected[name], actual[name]);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static byte[] Utf8(string text) => new UTF8Encoding(false, true).GetBytes(text);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
