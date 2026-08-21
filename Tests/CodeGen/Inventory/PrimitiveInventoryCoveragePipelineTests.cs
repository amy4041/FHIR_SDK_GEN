using System.Text.Json;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Inventory;

public sealed class PrimitiveInventoryCoveragePipelineTests
{
    private const string FhirVersion = "5.0.0";

    private readonly PrimitiveInventoryCoveragePipeline _pipeline = new();

    [Fact]
    public async Task BuildAsync_WithOfficialInputs_ReturnsCompleteCoverage()
    {
        var result = await _pipeline.BuildAsync(
            GetDefinitionsPath(),
            GetPolicyPath(),
            FhirVersion);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var coverage = Assert.IsType<PrimitiveInventoryPolicyCoverage>(result.Value);
        Assert.Equal(21, coverage.Matches.Count);
        Assert.Equal(
            17,
            coverage.Matches.Count(match => match.Policy.IsSupported));
        Assert.Equal(
            4,
            coverage.Matches.Count(match => !match.Policy.IsSupported));
        Assert.Equal(
            Path.GetFullPath(GetPolicyPath()),
            coverage.Policy.SourceFile);
    }

    [Fact]
    public async Task BuildAsync_WhenDefinitionsFail_ReturnsOnlyLoaderDiagnostics()
    {
        var result = await _pipeline.BuildAsync(
            Path.Combine(GetDefinitionsPath(), "missing"),
            Path.Combine(AppContext.BaseDirectory, "missing-policy.json"),
            FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.InvalidInput, diagnostic.Code);
    }

    [Fact]
    public async Task BuildAsync_WhenPolicyLoadFails_ReturnsLoaderDiagnostics()
    {
        var missingPolicyPath = Path.Combine(
            AppContext.BaseDirectory,
            "missing-policy.json");

        var result = await _pipeline.BuildAsync(
            GetDefinitionsPath(),
            missingPolicyPath,
            FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.PrimitivePolicyReadFailure,
            diagnostic.Code);
    }

    [Fact]
    public async Task BuildAsync_WhenInventoryBuildFails_ReturnsBuilderDiagnostics()
    {
        using var directory = new TemporaryDirectory();
        var booleanFixture = Path.Combine(
            GetDefinitionsPath(),
            "StructureDefinition-boolean.json");
        directory.CopyFrom(booleanFixture, "a.json");
        directory.CopyFrom(booleanFixture, "z.json");

        var result = await _pipeline.BuildAsync(
            directory.Path,
            Path.Combine(AppContext.BaseDirectory, "missing-policy.json"),
            FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.All(
            result.Diagnostics,
            diagnostic => Assert.Equal(
                GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry,
                diagnostic.Code));
    }

    [Fact]
    public async Task BuildAsync_WhenPolicyValidationFails_ReturnsValidatorDiagnostics()
    {
        using var directory = new TemporaryDirectory();
        var invalidPolicyPath = await directory.WriteAsync(
            "invalid-policy.json",
            """
            {
              "schemaVersion": 1,
              "policyVersion": "not-semver",
              "primitives": []
            }
            """);

        var result = await _pipeline.BuildAsync(
            GetDefinitionsPath(),
            invalidPolicyPath,
            FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.InvalidPrimitivePolicy);
        Assert.All(
            result.Diagnostics,
            diagnostic => Assert.Equal(
                Path.GetFullPath(invalidPolicyPath),
                diagnostic.SourceFile));
    }

    [Fact]
    public async Task BuildAsync_WhenCoverageFails_ReturnsJoinerDiagnostics()
    {
        using var directory = new TemporaryDirectory();
        var policyDocument = await LoadRepositoryPolicyDocumentAsync();
        var extraEntry = new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = "sample",
            Canonical = "http://hl7.org/fhir/StructureDefinition/sample",
            FhirVersion = FhirVersion,
            SupportStatus = "unsupported",
            UnsupportedReason = "No approved Runtime contract."
        };
        var modifiedPolicy = new PrimitiveGenerationPolicyDocument
        {
            SchemaVersion = policyDocument.SchemaVersion,
            PolicyVersion = policyDocument.PolicyVersion,
            FhirVersion = policyDocument.FhirVersion,
            RuntimeContractVersion = policyDocument.RuntimeContractVersion,
            PrimitiveNamespace = policyDocument.PrimitiveNamespace,
            Primitives = [.. policyDocument.Primitives!, extraEntry]
        };
        var policyPath = await directory.WriteAsync(
            "extra-policy.json",
            JsonSerializer.Serialize(modifiedPolicy));

        var result = await _pipeline.BuildAsync(
            GetDefinitionsPath(),
            policyPath,
            FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.ExtraPrimitivePolicyEntry,
            diagnostic.Code);
        Assert.Equal(Path.GetFullPath(policyPath), diagnostic.SourceFile);
    }

    [Fact]
    public async Task BuildAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _pipeline.BuildAsync(
                GetDefinitionsPath(),
                GetPolicyPath(),
                FhirVersion,
                cancellationSource.Token));
    }

    private static string GetDefinitionsPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Primitives",
            "R5");
    }

    private static string GetPolicyPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "primitive-generation-policy.json");
    }

    private static async Task<PrimitiveGenerationPolicyDocument>
        LoadRepositoryPolicyDocumentAsync()
    {
        var result = await new PrimitiveGenerationPolicyLoader().LoadAsync(
            GetPolicyPath());

        Assert.True(result.IsSuccess);
        return Assert.IsType<PrimitiveGenerationPolicyDocument>(result.Value);
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

        public string CopyFrom(string sourceFile, string fileName)
        {
            var destination = System.IO.Path.Combine(Path, fileName);
            File.Copy(sourceFile, destination);
            return destination;
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
