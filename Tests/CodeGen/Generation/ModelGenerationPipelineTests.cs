using System.Text.Json;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class ModelGenerationPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyFhirSdk-C7", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildAsync_OfficialFullScope_IsDeterministicAndComplete()
    {
        Directory.CreateDirectory(_root);
        var pipeline = new ModelGenerationPipeline(_root);
        var options = Options([]);

        var first = await pipeline.BuildAsync(options);
        var second = await pipeline.BuildAsync(options);

        Assert.True(first.IsSuccess, Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, Describe(second.Diagnostics));
        var firstBatch = Assert.IsType<ModelGenerationBatch>(first.Value);
        var secondBatch = Assert.IsType<ModelGenerationBatch>(second.Value);
        Assert.Equal(firstBatch.Artifacts.Select(x => (x.FileName, x.Content)),
            secondBatch.Artifacts.Select(x => (x.FileName, x.Content)));
        Assert.Equal("full", firstBatch.Manifest.Scope);
        Assert.Equal(firstBatch.Sources.Count, firstBatch.Manifest.Artifacts.Count);
        Assert.Contains(firstBatch.Sources, x => x.FileName == "Generated/R5/Resources/Patient/Patient.g.cs");
        Assert.Contains(firstBatch.Artifacts, x => x.FileName == ModelGenerationManifestModel.FileName);
    }

    [Fact]
    public async Task BuildAsync_ReorderedSelectedCanonicals_ProducesIdenticalBatch()
    {
        Directory.CreateDirectory(_root);
        var pipeline = new ModelGenerationPipeline(_root);
        var patient = "http://hl7.org/fhir/StructureDefinition/Patient";
        var observation = "http://hl7.org/fhir/StructureDefinition/Observation";

        var first = await pipeline.BuildAsync(Options([patient, observation]));
        var second = await pipeline.BuildAsync(Options([observation, patient]));

        Assert.True(first.IsSuccess, Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, Describe(second.Diagnostics));
        Assert.Equal(first.Value!.Artifacts.Select(x => (x.FileName, x.Content)),
            second.Value!.Artifacts.Select(x => (x.FileName, x.Content)));
        Assert.Equal("selected", first.Value.Manifest.Scope);
    }

    [Fact]
    public async Task BuildAsync_PolicyCheckoutNewlines_ProducesIdenticalManifest()
    {
        Directory.CreateDirectory(_root);
        var lfRoot = Path.Combine(_root, "policies-lf");
        var crlfRoot = Path.Combine(_root, "policies-crlf");
        await CopyPoliciesAsync(lfRoot, "\n");
        await CopyPoliciesAsync(crlfRoot, "\r\n");
        var selected = new[] { "http://hl7.org/fhir/StructureDefinition/Patient" };
        var pipeline = new ModelGenerationPipeline(_root);

        var lf = await pipeline.BuildAsync(WithPolicyRoot(Options(selected), lfRoot));
        var crlf = await pipeline.BuildAsync(WithPolicyRoot(Options(selected), crlfRoot));

        Assert.True(lf.IsSuccess, Describe(lf.Diagnostics));
        Assert.True(crlf.IsSuccess, Describe(crlf.Diagnostics));
        Assert.Equal(
            lf.Value!.Artifacts.Select(artifact => (artifact.FileName, artifact.Content)),
            crlf.Value!.Artifacts.Select(artifact => (artifact.FileName, artifact.Content)));
    }

    [Fact]
    public async Task GenerateAsync_SelectedScope_CommitsNestedBatchAndManifest()
    {
        Directory.CreateDirectory(_root);
        var output = Path.Combine(_root, "output");
        var options = Options(["http://hl7.org/fhir/StructureDefinition/Patient"]) with { OutputPath = output };

        var result = await new ModelGenerationPipeline(_root).GenerateAsync(options);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var manifestPath = Path.Combine(
            output,
            ModelGenerationManifestModel.FileName.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(manifestPath));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal("selected", manifest.RootElement.GetProperty("generationScope").GetProperty("mode").GetString());
        Assert.True(File.Exists(Path.Combine(output, "Generated", "R5", "Resources", "Patient", "Patient.g.cs")));
    }

    [Fact]
    public async Task GenerateAsync_InvalidScope_PreservesExistingOutput()
    {
        Directory.CreateDirectory(_root);
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        var marker = Path.Combine(output, "existing.txt");
        await File.WriteAllTextAsync(marker, "keep");
        var options = Options(["http://hl7.org/fhir/StructureDefinition/NotAType"]) with { OutputPath = output };

        var result = await new ModelGenerationPipeline(_root).GenerateAsync(options);

        Assert.False(result.IsSuccess);
        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
    }

    [Fact]
    public async Task GenerateAsync_CanceledBeforeBuild_PreservesExistingOutput()
    {
        Directory.CreateDirectory(_root);
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        var marker = Path.Combine(output, "existing.txt");
        await File.WriteAllTextAsync(marker, "keep");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ModelGenerationPipeline(_root).GenerateAsync(
                Options([]) with { OutputPath = output }, cancellation.Token));

        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
    }

    private ModelGenerationOptions Options(IReadOnlyList<string> selected)
    {
        string Policy(string name) => Path.Combine(AppContext.BaseDirectory, "Policy", name);
        return new ModelGenerationOptions(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz"),
            Path.Combine(_root, "output"), "hl7.fhir.r5.core", "5.0.0", "5.0.0",
            Policy("primitive-generation-policy.json"), Policy("r5-model-ownership-policy.json"),
            new ModelIrPolicyPaths(Policy("r5-model-naming-policy.json"), Policy("r5-backbone-policy.json"), Policy("r5-choice-open-type-policy.json")),
            Policy("r5-validation-capability-policy.json"), selected, ModelGenerationPipeline.DefaultCodeGenVersion);
    }

    private static ModelGenerationOptions WithPolicyRoot(
        ModelGenerationOptions options,
        string policyRoot) =>
        options with
        {
            PrimitivePolicyPath = Path.Combine(policyRoot, "primitive-generation-policy.json"),
            OwnershipPolicyPath = Path.Combine(policyRoot, "r5-model-ownership-policy.json"),
            ModelIrPolicyPaths = new ModelIrPolicyPaths(
                Path.Combine(policyRoot, "r5-model-naming-policy.json"),
                Path.Combine(policyRoot, "r5-backbone-policy.json"),
                Path.Combine(policyRoot, "r5-choice-open-type-policy.json")),
            ValidationPolicyPath = Path.Combine(policyRoot, "r5-validation-capability-policy.json")
        };

    private static async Task CopyPoliciesAsync(string destination, string newline)
    {
        Directory.CreateDirectory(destination);
        foreach (var name in new[]
        {
            "primitive-generation-policy.json",
            "r5-model-ownership-policy.json",
            "r5-model-naming-policy.json",
            "r5-backbone-policy.json",
            "r5-choice-open-type-policy.json",
            "r5-validation-capability-policy.json"
        })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Policy", name));
            var normalized = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", newline, StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(destination, name), normalized);
        }
    }

    private static string Describe(IEnumerable<MyFhirSdk.CodeGen.Diagnostics.GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(x => $"{x.Code}: {x.Message}"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
