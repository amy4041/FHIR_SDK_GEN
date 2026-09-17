using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class PrimitivePackageGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyFhirSdk-PrimitivePackage", Guid.NewGuid().ToString("N"));
    private static string DirectoryInput => Path.Combine(AppContext.BaseDirectory, "Fixtures", "StructureDefinitions", "Primitives", "R5");
    private static string ArchiveInput => Path.Combine(AppContext.BaseDirectory, "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz");
    private static string Policy => Path.Combine(AppContext.BaseDirectory, "Policy", "primitive-generation-policy.json");
    private static GeneratorCommandLineParser Parser() => new(new ToolAssetResolver(AppContext.BaseDirectory));

    public PrimitivePackageGenerationTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cli_ClassifiesInputAndStillRequiresExplicitPolicy(bool archive)
    {
        var args = Arguments(archive ? ArchiveInput : DirectoryInput);
        var parsed = Parser().Parse(args);
        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(archive ? PrimitiveDefinitionInputKind.PackageArchive : PrimitiveDefinitionInputKind.Directory,
            parsed.PrimitiveOptions!.InputKind);
        var index = Array.IndexOf(args, "--policy");
        var missing = Parser().Parse(args.Where((_, position) => position != index && position != index + 1).ToArray());
        Assert.Equal("Required option '--policy' was not provided.", missing.Error);
    }

    [Theory]
    [InlineData("definitions.json", true)]
    [InlineData("package.zip", true)]
    [InlineData("missing.tgz", false)]
    [InlineData("missing-directory", false)]
    public void Cli_RejectsUnsupportedOrMissingInput(string name, bool create)
    {
        var input = Path.Combine(_root, name);
        if (create) File.WriteAllText(input, "{}");
        var parsed = Parser().Parse(Arguments(input));
        Assert.False(parsed.IsSuccess);
        Assert.Contains("existing directory or .tgz file", parsed.Error);
    }

    [Fact]
    public void Classifier_UsesExistingDirectoryBeforeExtensionAndRejectsKindMismatch()
    {
        var input = Path.Combine(_root, "definitions.tgz");
        Directory.CreateDirectory(input);
        Assert.Equal(PrimitiveDefinitionInputKind.Directory, PrimitiveDefinitionInput.Resolve(input).Value!.Kind);
        Assert.False(PrimitiveDefinitionInput.Resolve(input, PrimitiveDefinitionInputKind.PackageArchive).IsSuccess);
    }

    [Fact]
    public async Task Cli_ArchiveAndDirectoryProduceIdenticalCompleteOutput()
    {
        var archiveOutput = Path.Combine(_root, "archive");
        var directoryOutput = Path.Combine(_root, "directory");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new GeneratorCli(output, error, Parser(), primitivePipeline: CodeGenTestRuntime.CreatePrimitivePipeline());
        Assert.Equal(0, await cli.RunAsync(Arguments(ArchiveInput, archiveOutput)));
        Assert.Equal(0, await cli.RunAsync(Arguments(DirectoryInput, directoryOutput)));
        Assert.Equal(0, await cli.RunAsync(Arguments(ArchiveInput, archiveOutput)));
        Assert.Empty(error.ToString());
        var names = Directory.GetFiles(directoryOutput).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(22, names.Length);
        Assert.Equal(names, Directory.GetFiles(archiveOutput).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var name in names) Assert.Equal(File.ReadAllBytes(Path.Combine(directoryOutput, name!)),
            File.ReadAllBytes(Path.Combine(archiveOutput, name!)));
    }

    [Theory]
    [InlineData("name", "other.package")]
    [InlineData("version", "5.0.1")]
    [InlineData("type", "IG")]
    [InlineData("fhirVersions", "4.0.1")]
    public async Task Pipeline_ValidatesActualArchiveIdentityBeforeWriting(string field, string value)
    {
        var metadata = JsonNode.Parse("""{"name":"hl7.fhir.r5.core","version":"5.0.0","type":"Core","fhirVersions":["5.0.0"]}""")!;
        metadata[field] = field == "fhirVersions" ? new JsonArray(value) : JsonValue.Create(value);
        var archive = WriteArchive(metadata.ToJsonString(), File.ReadAllText(Path.Combine(DirectoryInput, "StructureDefinition-string.json")));
        var result = await CodeGenTestRuntime.CreatePrimitivePipeline().GenerateAsync(Options(archive));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == GeneratorDiagnosticCodes.DefinitionPackageIdentityMismatch);
        Assert.False(Directory.Exists(Path.Combine(_root, "output")));
    }

    [Fact]
    public async Task Pipeline_PackageWithoutPrimitiveFails()
    {
        var archive = WriteArchive(null, File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "StructureDefinitions", "Valid", "StructureDefinition-Address.json")));
        var result = await CodeGenTestRuntime.CreatePrimitivePipeline().BuildAsync(Options(archive));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == GeneratorDiagnosticCodes.InvalidPrimitiveInventory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_BadArchivePreservesExistingOutput(bool truncated)
    {
        var archive = Path.Combine(_root, "bad.tgz");
        File.WriteAllBytes(archive, truncated ? File.ReadAllBytes(ArchiveInput)[..100] : Encoding.UTF8.GetBytes("not gzip"));
        var options = Options(archive);
        Directory.CreateDirectory(options.OutputPath);
        var marker = Path.Combine(options.OutputPath, "keep.txt");
        File.WriteAllText(marker, "keep");
        var result = await CodeGenTestRuntime.CreatePrimitivePipeline().GenerateAsync(options);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == GeneratorDiagnosticCodes.DefinitionPackageReadFailure);
        Assert.Equal("keep", File.ReadAllText(marker));
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("directory")]
    [InlineData("policy")]
    public async Task Pipeline_ProtectsInvocationInputsEvenForDirectCallers(string asset)
    {
        var output = Path.Combine(_root, "protected");
        Directory.CreateDirectory(output);
        var input = ArchiveInput;
        var policy = Policy;
        if (asset == "archive")
        {
            input = Path.Combine(output, "core.tgz");
            File.Copy(ArchiveInput, input);
        }
        else if (asset == "directory")
        {
            input = Path.Combine(output, "definitions");
            Directory.CreateDirectory(input);
            foreach (var file in Directory.GetFiles(DirectoryInput, "*.json")) File.Copy(file, Path.Combine(input, Path.GetFileName(file)));
        }
        else
        {
            policy = Path.Combine(output, "policy.json");
            File.Copy(Policy, policy);
        }
        var marker = Path.Combine(output, "keep.txt");
        File.WriteAllText(marker, "keep");
        var result = await CodeGenTestRuntime.CreatePrimitivePipeline().GenerateAsync(Options(input) with { PolicyPath = policy, OutputPath = output });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == GeneratorDiagnosticCodes.UnsafeOutputPath);
        Assert.Equal("keep", File.ReadAllText(marker));
        Assert.True(File.Exists(input) || Directory.Exists(input));
        Assert.True(File.Exists(policy));
        Assert.Empty(Directory.GetDirectories(_root, ".*.staging-*"));
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
    public async Task Pipeline_PreservesExplicitPolicyPreflightForBothInputs(bool archive, string fault, string code)
    {
        var policy = Path.Combine(_root, "policy.json");
        if (fault == "corrupt") File.WriteAllText(policy, "{");
        if (fault == "hash") File.WriteAllText(policy, File.ReadAllText(Policy) + "\n");
        if (fault == "version")
        {
            var document = JsonNode.Parse(File.ReadAllText(Policy))!;
            document["policyVersion"] = "2.0.0";
            File.WriteAllText(policy, document.ToJsonString());
        }
        var result = await CodeGenTestRuntime.CreatePrimitivePipeline().GenerateAsync(
            Options(archive ? ArchiveInput : DirectoryInput) with { PolicyPath = policy });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == code);
        Assert.False(Directory.Exists(Path.Combine(_root, "output")));
    }

    private PrimitiveGenerationOptions Options(string input) => new(input, Policy, Path.Combine(_root, "output"),
        "5.0.0", "hl7.fhir.r5.core", "5.0.0", PrimitiveGenerationPipeline.DefaultCodeGenVersion);

    private string[] Arguments(string input, string? output = null) =>
        ["--mode", "primitive", "--input", input, "--policy", Policy, "--output", output ?? Path.Combine(_root, "output"),
         "--fhir-version", "5.0.0", "--package-id", "hl7.fhir.r5.core", "--package-version", "5.0.0"];

    private string WriteArchive(string? metadata, string definition)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tgz");
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionMode.Compress);
        using var writer = new TarWriter(gzip);
        foreach (var (name, json) in new[] {
            ("package/package.json", metadata ?? """{"name":"hl7.fhir.r5.core","version":"5.0.0","type":"Core","fhirVersions":["5.0.0"]}"""),
            ("package/definition.json", definition) })
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = content });
        }
        return path;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
