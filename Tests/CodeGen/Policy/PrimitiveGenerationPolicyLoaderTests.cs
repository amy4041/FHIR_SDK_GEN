using System.Text.Json;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class PrimitiveGenerationPolicyLoaderTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        $"MyFhirSdk-PrimitivePolicyTests-{Guid.NewGuid():N}");

    public PrimitiveGenerationPolicyLoaderTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task LoadAsync_WithRepositoryPolicy_LoadsVersionedDocument()
    {
        var loader = new PrimitiveGenerationPolicyLoader();

        var result = await loader.LoadAsync(GetRepositoryPolicyPath());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var policy = Assert.IsType<PrimitiveGenerationPolicyDocument>(result.Value);
        Assert.Equal(1, policy.SchemaVersion);
        Assert.Equal("1.1.0", policy.PolicyVersion);
        Assert.Equal("5.0.0", policy.FhirVersion);
        Assert.Equal("phase-a-v1+c4-primitives-v1", policy.RuntimeContractVersion);
        Assert.Equal("MyFhirSdk.Primitives", policy.PrimitiveNamespace);
        Assert.Equal(21, policy.Primitives?.Count);
    }

    [Fact]
    public async Task PolicyDocument_SerializeThenLoad_PreservesSchemaData()
    {
        var loader = new PrimitiveGenerationPolicyLoader();
        var originalResult = await loader.LoadAsync(GetRepositoryPolicyPath());
        var original = Assert.IsType<PrimitiveGenerationPolicyDocument>(
            originalResult.Value);
        var path = await WriteAsync(
            "round-trip.json",
            JsonSerializer.Serialize(original));

        var roundTripResult = await loader.LoadAsync(path);

        Assert.True(roundTripResult.IsSuccess);
        var roundTrip = Assert.IsType<PrimitiveGenerationPolicyDocument>(
            roundTripResult.Value);
        Assert.Equal(original.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(original.PolicyVersion, roundTrip.PolicyVersion);
        Assert.Equal(original.FhirVersion, roundTrip.FhirVersion);
        Assert.Equal(original.RuntimeContractVersion, roundTrip.RuntimeContractVersion);
        Assert.Equal(original.PrimitiveNamespace, roundTrip.PrimitiveNamespace);
        Assert.Equal(
            original.Primitives?.Select(entry => entry?.FhirTypeName),
            roundTrip.Primitives?.Select(entry => entry?.FhirTypeName));
    }

    [Fact]
    public async Task LoadAsync_WithMalformedJson_ReturnsFsg0013()
    {
        var path = await WriteAsync("malformed.json", "{");
        var loader = new PrimitiveGenerationPolicyLoader();

        var result = await loader.LoadAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.PrimitivePolicyReadFailure,
            diagnostic.Code);
        Assert.Equal(Path.GetFullPath(path), diagnostic.SourceFile);
    }

    [Fact]
    public async Task LoadAsync_WithUnknownPropertyRejectsUnversionedSchemaExtension()
    {
        var path = await WriteAsync(
            "unknown-property.json",
            """
            {
              "schemaVersion": 1,
              "policyVersion": "1.0.0",
              "fhirVersion": "5.0.0",
              "runtimeContractVersion": "phase-a-v1",
              "primitiveNamespace": "MyFhirSdk.Primitives",
              "primitives": [],
              "unexpected": true
            }
            """);
        var loader = new PrimitiveGenerationPolicyLoader();

        var result = await loader.LoadAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            "unexpected",
            Assert.Single(result.Diagnostics).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WithMissingFileReturnsFsg0013()
    {
        var path = Path.Combine(_testRoot, "missing.json");
        var loader = new PrimitiveGenerationPolicyLoader();

        var result = await loader.LoadAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.PrimitivePolicyReadFailure,
            diagnostic.Code);
        Assert.Equal(Path.GetFullPath(path), diagnostic.SourceFile);
    }

    [Fact]
    public async Task LoadAsync_WhenCancelledPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var loader = new PrimitiveGenerationPolicyLoader();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => loader.LoadAsync(
                GetRepositoryPolicyPath(),
                cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private async Task<string> WriteAsync(string fileName, string content)
    {
        var path = Path.Combine(_testRoot, fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static string GetRepositoryPolicyPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "primitive-generation-policy.json");
    }
}
