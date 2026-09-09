using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Compatibility;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Compatibility;

public sealed class GenerationCompatibilityServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "MyFhirSdk-CompatibilityTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidateAsync_BaselineModelMatrix_ReturnsManifestProvenance()
    {
        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(ModelRequest());

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var provenance = Assert.IsType<GenerationManifestProvenance>(result.Value);
        Assert.Equal(1, provenance.CompatibilitySchemaVersion);
        Assert.Equal("exact", provenance.CompatibilityVersionPolicy);
        Assert.Equal("MyFhirSdk.CodeGen.Tool", provenance.ToolPackageId);
        Assert.Equal("1.0.0", provenance.ToolVersion);
        Assert.Equal("phase-a-v1+c4-primitives-v1", provenance.RuntimeDescriptorVersion);
        Assert.Equal(64, provenance.RuntimeDescriptorSha256.Length);
        Assert.Equal(CodeGenTestRuntime.RuntimeReferences.ReferenceSha256,
            provenance.CompilerReferenceSha256);
        Assert.Equal("net9.0", provenance.TargetFramework);
    }

    [Theory]
    [InlineData("toolVersion", "2.0.0", GeneratorDiagnosticCodes.IncompatibleToolVersion)]
    [InlineData("contractVersion", "phase-x-v2", GeneratorDiagnosticCodes.IncompatibleRuntimeContractVersion)]
    [InlineData("targetFramework", "net10.0", GeneratorDiagnosticCodes.UnsupportedTargetFramework)]
    [InlineData("compatibilitySchemaVersion", "2", GeneratorDiagnosticCodes.IncompatibleCompatibilitySchema)]
    [InlineData("versionPolicy", "range", GeneratorDiagnosticCodes.IncompatibleCompatibilitySchema)]
    public async Task ValidateAsync_IncompatibleContractDimension_ReturnsDedicatedDiagnostic(
        string dimension,
        string value,
        string expectedCode)
    {
        var contract = await LoadModifiedContractAsync(root =>
        {
            if (dimension == "toolVersion")
            {
                root["compatibility"]!["toolVersion"] = value;
            }
            else if (dimension == "contractVersion")
            {
                root["contractVersion"] = value;
            }
            else if (dimension == "targetFramework")
            {
                root["targetFramework"] = value;
                root["compilerReference"]!["targetFramework"] = value;
            }
            else if (dimension == "compatibilitySchemaVersion")
            {
                root["compatibility"]!["schemaVersion"] = int.Parse(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                root["compatibility"]!["versionPolicy"] = value;
            }
        });

        var result = await CreateService(contract).ValidateAsync(ModelRequest());

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.StartsWith("<compatibility:", diagnostic.SourceFile));
    }

    [Theory]
    [InlineData("CodeGenVersion", GeneratorDiagnosticCodes.IncompatibleCodeGenVersion)]
    [InlineData("FhirPackageVersion", GeneratorDiagnosticCodes.IncompatibleFhirPackage)]
    public async Task ValidateAsync_IncompatibleInvocationDimension_ReturnsDedicatedDiagnostic(
        string dimension,
        string expectedCode)
    {
        var request = dimension == "CodeGenVersion"
            ? ModelRequest() with { CodeGenVersion = "2.0.0" }
            : ModelRequest() with { FhirPackageVersion = "5.0.1" };

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(request);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public async Task ValidateAsync_ReferenceSetFromDifferentDescriptor_ReturnsDedicatedDiagnostic()
    {
        var republishedContract = await LoadModifiedContractAsync(_ => { });

        var result = await CreateService(republishedContract)
            .ValidateAsync(ModelRequest());

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.RuntimeContractReferenceMismatch,
            diagnostic.Code);
        Assert.Equal(
            "<compatibility:runtime-reference-contract-sha256>",
            diagnostic.SourceFile);
    }

    [Fact]
    public async Task ValidateAsync_ModifiedPrimitivePolicy_ReturnsDedicatedDiagnostic()
    {
        Directory.CreateDirectory(_root);
        var policyPath = Path.Combine(_root, "primitive-policy.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(Policy(
            "primitive-generation-policy.json")))!;
        root["policyVersion"] = "1.2.0";
        await File.WriteAllTextAsync(policyPath, root.ToJsonString());

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(ModelRequest() with { PrimitivePolicyPath = policyPath });

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy);
    }

    [Fact]
    public async Task ValidateAsync_ModifiedModelPolicy_ReturnsDedicatedDiagnostic()
    {
        Directory.CreateDirectory(_root);
        var policyPath = Path.Combine(_root, "model-naming.json");
        await File.WriteAllTextAsync(policyPath,
            await File.ReadAllTextAsync(Policy("r5-model-naming-policy.json")) + "\n ");
        var request = ModelRequest();
        request = request with
        {
            ModelPolicies = request.ModelPolicies
                .Select(asset => asset.LogicalName == "model-naming"
                    ? asset with { Path = policyPath }
                    : asset)
                .ToArray()
        };

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(request);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.IncompatibleModelPolicy);
    }

    [Fact]
    public async Task ValidateAsync_MissingModelPolicy_ReturnsLogicalAssetDiagnostic()
    {
        var request = ModelRequest();
        request = request with
        {
            ModelPolicies = request.ModelPolicies
                .Select(asset => asset.LogicalName == "model-naming"
                    ? asset with { Path = Path.Combine(_root, "missing.json") }
                    : asset)
                .ToArray()
        };

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(request);

        var diagnostic = Assert.Single(result.Diagnostics, item =>
            item.Code == GeneratorDiagnosticCodes.PackagedAssetMissing);
        Assert.Equal("<asset:model-policy:model-naming>", diagnostic.SourceFile);
        Assert.DoesNotContain(_root, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_MissingPrimitivePolicy_ReturnsLogicalAssetDiagnostic()
    {
        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(ModelRequest() with
            {
                PrimitivePolicyPath = Path.Combine(_root, "missing-primitive.json")
            });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.PackagedAssetMissing, diagnostic.Code);
        Assert.Equal("<asset:primitive-policy>", diagnostic.SourceFile);
        Assert.DoesNotContain(_root, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_MalformedPrimitivePolicy_ReturnsLogicalCorruptDiagnostic()
    {
        Directory.CreateDirectory(_root);
        var policyPath = Path.Combine(_root, "malformed-policy.json");
        await File.WriteAllTextAsync(policyPath, "{ not-json }");

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(ModelRequest() with { PrimitivePolicyPath = policyPath });

        AssertLogicalCorruptDiagnostic(result.Diagnostics);
    }

    [Fact]
    public async Task ValidateAsync_InvalidUtf8PrimitivePolicy_ReturnsLogicalCorruptDiagnostic()
    {
        Directory.CreateDirectory(_root);
        var policyPath = Path.Combine(_root, "invalid-utf8-policy.json");
        await File.WriteAllBytesAsync(policyPath, [0x7b, 0xff, 0x7d]);

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(ModelRequest() with { PrimitivePolicyPath = policyPath });

        AssertLogicalCorruptDiagnostic(result.Diagnostics);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static GenerationCompatibilityService CreateService(
        RuntimeContractView contract) =>
        new(contract, CodeGenTestRuntime.RuntimeReferences);

    private static GenerationCompatibilityRequest ModelRequest() =>
        new(
            "1.0.0",
            "hl7.fhir.r5.core",
            "5.0.0",
            "5.0.0",
            Policy("primitive-generation-policy.json"),
            [
                new("backbone", Policy("r5-backbone-policy.json")),
                new("choice-open-type", Policy("r5-choice-open-type-policy.json")),
                new("model-naming", Policy("r5-model-naming-policy.json")),
                new("model-ownership", Policy("r5-model-ownership-policy.json")),
                new("validation-capability", Policy("r5-validation-capability-policy.json"))
            ]);

    private async Task<RuntimeContractView> LoadModifiedContractAsync(
        Action<JsonNode> modify)
    {
        Directory.CreateDirectory(_root);
        var node = JsonNode.Parse(await File.ReadAllTextAsync(Policy(
            "runtime-contract.json")))!;
        modify(node);
        var path = Path.Combine(_root, $"contract-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, node.ToJsonString());
        var result = await new RuntimeContractLoader().LoadAsync(path);
        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        return Assert.IsType<RuntimeContractView>(result.Value);
    }

    private static string Policy(string name) => Path.Combine(
        AppContext.BaseDirectory,
        "Policy",
        name);

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"[{diagnostic.Code}] {diagnostic.SourceFile}: {diagnostic.Message}"));

    private void AssertLogicalCorruptDiagnostic(
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.PackagedAssetCorrupt, diagnostic.Code);
        Assert.Equal("<asset:primitive-policy>", diagnostic.SourceFile);
        Assert.DoesNotContain(_root, diagnostic.Message, StringComparison.Ordinal);
    }
}
