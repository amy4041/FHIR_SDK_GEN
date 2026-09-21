using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Writing;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class PrimitiveCliFailureMatrixTests : IDisposable
{
    private const string Metadata = """{"name":"hl7.fhir.r5.core","version":"5.0.0","type":"Core","fhirVersions":["5.0.0"]}""";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyFhirSdk-PrimitiveCliMatrix", Guid.NewGuid().ToString("N"));
    private static string Definitions => Path.Combine(AppContext.BaseDirectory, "Fixtures", "StructureDefinitions", "Primitives", "R5");
    private static string Package => Path.Combine(AppContext.BaseDirectory, "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz");
    private static string Policy => Path.Combine(AppContext.BaseDirectory, "Policy", "primitive-generation-policy.json");
    private static string Primitive => File.ReadAllText(Path.Combine(Definitions, "StructureDefinition-string.json"));

    public PrimitiveCliFailureMatrixTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingExplicitPolicy_ReturnsUsageErrorWithoutWriting(bool archive)
    {
        var args = Arguments(archive ? Package : Definitions);
        var index = Array.IndexOf(args, "--policy");
        await AssertFailureAsync(args.Where((_, i) => i != index && i != index + 1).ToArray(),
            1, "Required option '--policy' was not provided.");
    }

    [Theory]
    [InlineData(false, "--package-id", "other.package")]
    [InlineData(true, "--package-id", "other.package")]
    [InlineData(false, "--package-version", "5.0.1")]
    [InlineData(true, "--package-version", "5.0.1")]
    [InlineData(false, "--fhir-version", "4.0.1")]
    [InlineData(true, "--fhir-version", "4.0.1")]
    public async Task UnsupportedInvocationIdentity_ReturnsCompatibilityDiagnostic(bool archive, string option, string value)
    {
        var args = Arguments(archive ? Package : Definitions);
        args[Array.IndexOf(args, option) + 1] = value;
        await AssertFailureAsync(args, 2, $"[{GeneratorDiagnosticCodes.IncompatibleFhirPackage}]");
    }

    [Theory]
    [InlineData("missing-metadata")]
    [InlineData("duplicate-metadata")]
    [InlineData("corrupt-metadata")]
    [InlineData("corrupt-definition")]
    [InlineData("duplicate-entry")]
    [InlineData("rooted")]
    [InlineData("traversal")]
    [InlineData("backslash")]
    public async Task InvalidArchiveEntries_ReturnReadDiagnosticAndPreserveFilesystem(string fault)
    {
        var entries = new List<(string, string)> { ("package/package.json", Metadata), ("package/string.json", Primitive) };
        switch (fault)
        {
            case "missing-metadata": entries.RemoveAt(0); break;
            case "duplicate-metadata": entries.Add(entries[0]); break;
            case "corrupt-metadata": entries[0] = ("package/package.json", "{"); break;
            case "corrupt-definition": entries.Add(("package/broken.json", "{")); break;
            case "duplicate-entry": entries.Add(entries[1]); break;
            case "rooted": entries.Add((Path.Combine(_root, "escaped.json").Replace('\\', '/'), Primitive)); break;
            case "traversal": entries.Add(("package/../escaped.json", Primitive)); break;
            case "backslash": entries.Add(("package\\escaped.json", Primitive)); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        await AssertFailureAsync(Arguments(WriteArchive(entries)), 2,
            $"[{GeneratorDiagnosticCodes.DefinitionPackageReadFailure}]");
    }

    [Theory]
    [InlineData("resourceType", "Patient", GeneratorDiagnosticCodes.InvalidPrimitiveInventory, 2)]
    [InlineData("kind", "unknown-kind", GeneratorDiagnosticCodes.InvalidPrimitiveInventory, 3)]
    [InlineData("snapshot", null, GeneratorDiagnosticCodes.MissingSnapshot, 2)]
    [InlineData("differential", null, GeneratorDiagnosticCodes.MissingDifferential, 2)]
    public async Task InvalidPrimitiveShape_IsNotFilteredOut(string field, string? value, string code, int exitCode)
    {
        var broken = JsonNode.Parse(Primitive)!;
        broken[field] = value;
        var archive = WriteArchive([("package/package.json", Metadata), ("package/broken.json", broken.ToJsonString())]);
        // Unknown kind also produces UnsupportedDefinition, whose CLI exit code is 3.
        var error = await AssertFailureAsync(Arguments(archive), exitCode, $"[{code}]");
        Assert.Contains("package/broken.json", error);
        if (field == "kind") Assert.Contains($"[{GeneratorDiagnosticCodes.UnsupportedDefinition}]", error);
    }

    [Theory]
    [InlineData(false, "missing", GeneratorDiagnosticCodes.PackagedAssetMissing)]
    [InlineData(true, "missing", GeneratorDiagnosticCodes.PackagedAssetMissing)]
    [InlineData(false, "corrupt", GeneratorDiagnosticCodes.PackagedAssetCorrupt)]
    [InlineData(true, "corrupt", GeneratorDiagnosticCodes.PackagedAssetCorrupt)]
    [InlineData(false, "hash", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    [InlineData(true, "hash", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    [InlineData(false, "version", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    [InlineData(true, "version", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    public async Task ExplicitPolicyFailure_IsReportedByCliAndPreservesOutput(bool archive, string fault, string code)
    {
        var policy = Path.Combine(_root, "policy.json");
        if (fault == "corrupt") File.WriteAllText(policy, "{");
        if (fault == "hash") File.WriteAllText(policy, File.ReadAllText(Policy) + "\n");
        if (fault == "version")
        {
            var node = JsonNode.Parse(File.ReadAllText(Policy))!;
            node["policyVersion"] = "2.0.0";
            File.WriteAllText(policy, node.ToJsonString());
        }
        var args = Arguments(archive ? Package : Definitions);
        args[Array.IndexOf(args, "--policy") + 1] = policy;
        var error = await AssertFailureAsync(args, 2, $"[{code}]");
        if (fault is "hash" or "version") Assert.Contains(
            fault == "hash" ? "<compatibility:primitive-policy-sha256>" : "<compatibility:primitive-policy-version>", error);
        else Assert.Contains("<asset:primitive-policy>", error);
    }

    [Theory]
    [InlineData("non-gzip")]
    [InlineData("truncated-header")]
    [InlineData("truncated-tail")]
    public async Task BrokenArchive_ReturnsReadExitCode(string fault)
    {
        var path = Path.Combine(_root, "broken.tgz");
        var bytes = File.ReadAllBytes(Package);
        File.WriteAllBytes(path, fault switch
        {
            "non-gzip" => Encoding.UTF8.GetBytes("not gzip"),
            "truncated-header" => bytes[..100],
            "truncated-tail" => bytes[..^8],
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        });
        await AssertFailureAsync(Arguments(path), 2, $"[{GeneratorDiagnosticCodes.DefinitionPackageReadFailure}]");
    }

    [Fact]
    public async Task NoPrimitiveSpecializations_ReturnsInventoryFailure()
    {
        var complex = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "StructureDefinitions", "Valid", "StructureDefinition-Address.json"));
        var path = WriteArchive([("package/package.json", Metadata), ("package/Address.json", complex)]);
        await AssertFailureAsync(Arguments(path), 2, $"[{GeneratorDiagnosticCodes.InvalidPrimitiveInventory}]");
    }

    [Theory]
    [InlineData("type")]
    [InlineData("canonical")]
    public async Task DuplicatePrimitiveIdentity_HasOrderIndependentDiagnostics(string collision)
    {
        var second = JsonNode.Parse(Primitive)!;
        var third = JsonNode.Parse(Primitive)!;
        if (collision == "type")
        {
            second["url"] = "http://example.test/StructureDefinition/other-string";
            third["url"] = "http://example.test/StructureDefinition/third-string";
        }
        else
        {
            second["type"] = "other-string";
            third["type"] = "third-string";
        }
        var entries = new[] { ("package/package.json", Metadata), ("package/z.json", Primitive),
            ("package/a.json", second.ToJsonString()), ("package/m.json", third.ToJsonString()) };
        var path = WriteArchive(entries);
        var forward = await AssertFailureAsync(Arguments(path), 2, $"[{GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry}]");
        WriteArchive(entries.Reverse(), path);
        var reverse = await AssertFailureAsync(Arguments(path), 2, $"[{GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry}]");
        Assert.Equal(forward, reverse);
        var lines = forward.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Contains(collision == "type" ? "inventory FHIR type name" : "inventory canonical", line));
        Assert.Equal(lines.Order(StringComparer.Ordinal), lines);
        Assert.Contains("the first ordinal source is 'package/a.json'", forward);
    }

    [Theory]
    [InlineData(false, "tool")]
    [InlineData(true, "tool")]
    [InlineData(false, "asset")]
    [InlineData(true, "asset")]
    public async Task ProtectedToolPaths_ReturnSafetyExitCode(bool archive, string kind)
    {
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        var asset = Path.Combine(output, "runtime-contract.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Policy", "runtime-contract.json"), asset);
        var safety = new OutputSafetyContext(kind == "tool" ? output : AppContext.BaseDirectory,
            kind == "asset" ? [asset] : []);
        var pipeline = new PrimitiveGenerationPipeline(safety, CodeGenTestRuntime.RuntimeContract,
            CodeGenTestRuntime.CreateCompilationValidator());
        await AssertFailureAsync(Arguments(archive ? Package : Definitions), 5,
            $"[{GeneratorDiagnosticCodes.UnsafeOutputPath}]", pipeline);
    }

    private async Task<string> AssertFailureAsync(string[] args, int exitCode, string errorText,
        PrimitiveGenerationPipeline? pipeline = null)
    {
        var outputPath = args[Array.IndexOf(args, "--output") + 1];
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, "keep.txt"), "keep");
        var before = Snapshot();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new GeneratorCli(output, error, new GeneratorCommandLineParser(new ToolAssetResolver(AppContext.BaseDirectory)),
            primitivePipeline: pipeline ?? CodeGenTestRuntime.CreatePrimitivePipeline());
        Assert.Equal(exitCode, await cli.RunAsync(args));
        Assert.Contains(errorText, error.ToString());
        Assert.DoesNotContain("Generated ", output.ToString());
        var after = Snapshot();
        Assert.Equal(before.Keys, after.Keys);
        foreach (var name in before.Keys) Assert.Equal(before[name], after[name]);
        Assert.Empty(Directory.GetDirectories(_root, ".*.staging-*", SearchOption.AllDirectories));
        return error.ToString();
    }

    private SortedDictionary<string, byte[]> Snapshot() => new(Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(_root, path), File.ReadAllBytes), StringComparer.Ordinal);

    private string[] Arguments(string input) => ["--mode", "primitive", "--input", input, "--policy", Policy,
        "--output", Path.Combine(_root, "output"), "--fhir-version", "5.0.0", "--package-id", "hl7.fhir.r5.core", "--package-version", "5.0.0"];

    private string WriteArchive(IEnumerable<(string Name, string Json)> entries, string? path = null)
    {
        path ??= Path.Combine(_root, "input.tgz");
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionMode.Compress);
        using var writer = new TarWriter(gzip);
        foreach (var (name, json) in entries)
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = content });
        }
        return path;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
