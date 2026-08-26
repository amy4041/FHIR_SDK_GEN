using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class PrimitiveGenerationPipelineTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "MyFhirSdk-PrimitivePipelineTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GenerateAsync_OfficialR5Batch_WritesSourcesAndManifest()
    {
        Directory.CreateDirectory(_testRoot);
        var output = Path.Combine(_testRoot, "Generated", "R5", "Primitives");
        var result = await CreatePipeline().GenerateAsync(CreateOptions(output));

        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(19, result.Value.Count);
        Assert.Equal(19, Directory.EnumerateFiles(output).Count());
        Assert.Contains(
            Path.Combine(output, "PrimitiveRegistry.Composition.g.cs"),
            result.Value);

        var manifestPath = Path.Combine(
            output,
            "primitive-generation-manifest.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var root = manifest.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("hl7.fhir.r5.core", root.GetProperty("fhirPackageId").GetString());
        Assert.Equal("5.0.0", root.GetProperty("fhirPackageVersion").GetString());
        Assert.Equal("1.0.0", root.GetProperty("policyVersion").GetString());
        Assert.Equal("phase-a-v1", root.GetProperty("runtimeContractVersion").GetString());
        Assert.Equal("MyFhirSdk.Primitives", root.GetProperty("primitiveNamespace").GetString());
        Assert.Equal(21, root.GetProperty("primitives").GetArrayLength());
        Assert.Equal(18, root.GetProperty("artifacts").GetArrayLength());
        Assert.DoesNotContain(
            root.GetProperty("artifacts").EnumerateArray(),
            item => item.GetProperty("fileName").GetString() ==
                "primitive-generation-manifest.json");
        var artifactEntries = root.GetProperty("artifacts").EnumerateArray().ToArray();
        Assert.Equal(
            artifactEntries
                .Select(item => item.GetProperty("fileName").GetString())
                .Order(StringComparer.Ordinal),
            artifactEntries.Select(item => item.GetProperty("fileName").GetString()));
        Assert.All(artifactEntries, item =>
        {
            var fileName = item.GetProperty("fileName").GetString()!;
            var expectedHash = item.GetProperty("sha256").GetString();
            var actualHash = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(output, fileName))))
                .ToLowerInvariant();
            Assert.Equal(expectedHash, actualHash);
        });

        var unsupported = root.GetProperty("primitives").EnumerateArray()
            .Where(item => item.GetProperty("supportStatus").GetString() == "unsupported")
            .ToArray();
        Assert.Equal(4, unsupported.Length);
        Assert.All(unsupported, item => Assert.False(string.IsNullOrWhiteSpace(
            item.GetProperty("unsupportedReason").GetString())));
    }

    [Fact]
    public async Task BuildAsync_ReorderedDefinitionsAndPolicy_ProducesIdenticalArtifacts()
    {
        Directory.CreateDirectory(_testRoot);
        var definitions = Path.Combine(_testRoot, "definitions");
        CopyDefinitions(definitions, reverse: true);
        var reversedPolicy = Path.Combine(_testRoot, "reversed-policy.json");
        await WriteReversedPolicyAsync(reversedPolicy);
        var pipeline = CreatePipeline();

        var original = await pipeline.BuildAsync(CreateOptions(
            Path.Combine(_testRoot, "original")));
        var reordered = await pipeline.BuildAsync(CreateOptions(
            Path.Combine(_testRoot, "reordered"),
            definitions,
            reversedPolicy));

        Assert.True(original.IsSuccess, FormatDiagnostics(original.Diagnostics));
        Assert.True(reordered.IsSuccess, FormatDiagnostics(reordered.Diagnostics));
        var first = Assert.IsType<PrimitiveGenerationBatch>(original.Value);
        var second = Assert.IsType<PrimitiveGenerationBatch>(reordered.Value);
        Assert.Equal(first.Artifacts, second.Artifacts);
    }

    [Fact]
    public async Task GenerateAsync_SecondRunIsByteIdenticalAndRemovesStaleFile()
    {
        Directory.CreateDirectory(_testRoot);
        var output = Path.Combine(_testRoot, "generated");
        var pipeline = CreatePipeline();
        var options = CreateOptions(output);
        var first = await pipeline.GenerateAsync(options);
        Assert.True(first.IsSuccess, FormatDiagnostics(first.Diagnostics));
        var firstBytes = ReadBatch(output);
        await File.WriteAllTextAsync(Path.Combine(output, "stale.g.cs"), "stale");

        var second = await pipeline.GenerateAsync(options);

        Assert.True(second.IsSuccess, FormatDiagnostics(second.Diagnostics));
        Assert.False(File.Exists(Path.Combine(output, "stale.g.cs")));
        Assert.Equal(firstBytes, ReadBatch(output));
    }

    [Fact]
    public async Task GenerateAsync_CancelledBeforeCommit_PreservesPreviousOutput()
    {
        Directory.CreateDirectory(_testRoot);
        var output = Path.Combine(_testRoot, "generated");
        Directory.CreateDirectory(output);
        var marker = Path.Combine(output, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreatePipeline().GenerateAsync(CreateOptions(output), cancellation.Token));

        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
        Assert.Equal(["keep.txt"], Directory.EnumerateFiles(output).Select(Path.GetFileName));
    }

    [Fact]
    public async Task BuildAsync_OfficialR5Batch_MatchesCommittedGeneratedOutput()
    {
        Directory.CreateDirectory(_testRoot);
        var result = await CreatePipeline().BuildAsync(CreateOptions(
            Path.Combine(_testRoot, "unused-output")));
        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        var batch = Assert.IsType<PrimitiveGenerationBatch>(result.Value);
        var committedRoot = Path.Combine(
            AppContext.BaseDirectory,
            "CommittedGenerated",
            "R5",
            "Primitives");
        var committedFiles = Directory.EnumerateFiles(committedRoot)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            batch.Artifacts.Select(artifact => artifact.FileName),
            committedFiles.Select(Path.GetFileName));
        foreach (var artifact in batch.Artifacts)
        {
            var expectedBytes = Encoding.UTF8.GetBytes(
                NormalizeNewlines(artifact.Content));
            var actualBytes = await File.ReadAllBytesAsync(Path.Combine(
                committedRoot,
                artifact.FileName));
            Assert.Equal(expectedBytes, actualBytes);
        }
    }

    private static string NormalizeNewlines(string value) => value
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n');

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private PrimitiveGenerationPipeline CreatePipeline() => new(_testRoot);

    private static PrimitiveGenerationOptions CreateOptions(
        string output,
        string? definitions = null,
        string? policy = null) => new(
            definitions ?? GetDefinitionDirectory(),
            policy ?? GetPolicyPath(),
            output,
            "5.0.0",
            "hl7.fhir.r5.core",
            "5.0.0",
            PrimitiveGenerationPipeline.DefaultCodeGenVersion);

    private static string GetDefinitionDirectory() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "StructureDefinitions",
        "Primitives",
        "R5");

    private static string GetPolicyPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Policy",
        "primitive-generation-policy.json");

    private static void CopyDefinitions(string destination, bool reverse)
    {
        Directory.CreateDirectory(destination);
        var files = Directory.EnumerateFiles(GetDefinitionDirectory(), "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (reverse)
        {
            Array.Reverse(files);
        }

        foreach (var file in files)
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }

    private static async Task WriteReversedPolicyAsync(string destination)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(GetPolicyPath()))!
            .AsObject();
        var primitives = root["primitives"]!.AsArray();
        var reversed = primitives.Select(item => item!.DeepClone()).Reverse().ToArray();
        primitives.Clear();
        foreach (var item in reversed)
        {
            primitives.Add(item);
        }

        await File.WriteAllTextAsync(destination, root.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Dictionary<string, byte[]> ReadBatch(string output) =>
        Directory.EnumerateFiles(output)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetFileName(path)!,
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static string FormatDiagnostics(
        IEnumerable<MyFhirSdk.CodeGen.Diagnostics.GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics);
}
