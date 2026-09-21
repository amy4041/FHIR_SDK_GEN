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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAsync_CurrentMatrix_ReturnsManifestProvenance(bool primitive)
    {
        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(Request(primitive));

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var provenance = Assert.IsType<GenerationManifestProvenance>(result.Value);
        Assert.Equal(1, provenance.CompatibilitySchemaVersion);
        Assert.Equal("exact", provenance.CompatibilityVersionPolicy);
        Assert.Equal("MyFhirSdk.CodeGen.Tool", provenance.ToolPackageId);
        Assert.Equal("1.1.0", provenance.ToolVersion);
        Assert.Equal("1.1.0", provenance.CodeGenVersion);
        Assert.Equal("phase-a-v1+c4-primitives-v1", provenance.RuntimeDescriptorVersion);
        Assert.Equal(CodeGenTestRuntime.RuntimeContract.DescriptorSha256, provenance.RuntimeDescriptorSha256);
        Assert.Equal(CodeGenTestRuntime.RuntimeReferences.ReferenceSha256,
            provenance.CompilerReferenceSha256);
        Assert.Equal(GenerationCompatibilityMatrix.TargetFramework, provenance.TargetFramework);
    }

    [Theory]
    [InlineData("toolVersion", "2.0.0", GeneratorDiagnosticCodes.IncompatibleToolVersion, "tool-version")]
    [InlineData("toolVersion", "1.0.0", GeneratorDiagnosticCodes.IncompatibleToolVersion, "tool-version")]
    [InlineData("codeGenVersion", "1.0.0", GeneratorDiagnosticCodes.IncompatibleCodeGenVersion, "codegen-version")]
    [InlineData("codeGenVersion", "1.1.1", GeneratorDiagnosticCodes.IncompatibleCodeGenVersion, "codegen-version")]
    [InlineData("contractVersion", "phase-x-v2", GeneratorDiagnosticCodes.IncompatibleRuntimeContractVersion, "runtime-contract-version")]
    [InlineData("targetFramework", "net10.0", GeneratorDiagnosticCodes.UnsupportedTargetFramework, "target-framework")]
    [InlineData("compatibilitySchemaVersion", "2", GeneratorDiagnosticCodes.IncompatibleCompatibilitySchema, "compatibility-schema-version")]
    [InlineData("versionPolicy", "range", GeneratorDiagnosticCodes.IncompatibleCompatibilitySchema, "compatibility-version-policy")]
    public async Task ValidateAsync_IncompatibleContractDimension_ReturnsDedicatedDiagnostic(
        string dimension,
        string value,
        string expectedCode,
        string expectedDimension)
    {
        var contract = await LoadModifiedContractAsync(root =>
        {
            if (dimension is "toolVersion" or "codeGenVersion")
            {
                root["compatibility"]![dimension] = value;
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

        foreach (var primitive in new[] { false, true })
        {
            var result = await CreateService(contract).ValidateAsync(Request(primitive));
            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode &&
                diagnostic.SourceFile == $"<compatibility:{expectedDimension}>");
            Assert.All(result.Diagnostics, diagnostic =>
                Assert.StartsWith("<compatibility:", diagnostic.SourceFile));
        }
    }

    [Theory]
    [InlineData("CodeGenVersion", "1.0.0", GeneratorDiagnosticCodes.IncompatibleCodeGenVersion, "codegen-version")]
    [InlineData("CodeGenVersion", "1.1.1", GeneratorDiagnosticCodes.IncompatibleCodeGenVersion, "codegen-version")]
    [InlineData("CodeGenVersion", "1.1.0-preview.1", GeneratorDiagnosticCodes.IncompatibleCodeGenVersion, "codegen-version")]
    [InlineData("FhirPackageId", "HL7.fhir.r5.core", GeneratorDiagnosticCodes.IncompatibleFhirPackage, "fhir-package-id")]
    [InlineData("FhirPackageVersion", "5.0.1", GeneratorDiagnosticCodes.IncompatibleFhirPackage, "fhir-package-version")]
    [InlineData("FhirVersion", "4.0.1", GeneratorDiagnosticCodes.IncompatibleFhirPackage, "fhir-version")]
    public async Task ValidateAsync_IncompatibleInvocationDimension_ReturnsDedicatedDiagnostic(
        string dimension,
        string value,
        string expectedCode,
        string expectedDimension)
    {
        foreach (var primitive in new[] { false, true })
        {
            var baseline = Request(primitive);
            var request = dimension switch
            {
                "CodeGenVersion" => baseline with { CodeGenVersion = value },
                "FhirPackageId" => baseline with { FhirPackageId = value },
                "FhirPackageVersion" => baseline with { FhirPackageVersion = value },
                "FhirVersion" => baseline with { FhirVersion = value },
                _ => throw new ArgumentOutOfRangeException(nameof(dimension))
            };
            var result = await CreateService(CodeGenTestRuntime.RuntimeContract).ValidateAsync(request);
            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode &&
                diagnostic.SourceFile == $"<compatibility:{expectedDimension}>");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAsync_ReferenceSetFromDifferentDescriptor_ReturnsDedicatedDiagnostic(bool primitive)
    {
        var republishedContract = await LoadModifiedContractAsync(_ => { });

        var result = await CreateService(republishedContract)
            .ValidateAsync(Request(primitive));

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

    [Theory]
    [InlineData("backbone", "r5-backbone-policy.json")]
    [InlineData("choice-open-type", "r5-choice-open-type-policy.json")]
    [InlineData("model-naming", "r5-model-naming-policy.json")]
    [InlineData("model-ownership", "r5-model-ownership-policy.json")]
    [InlineData("validation-capability", "r5-validation-capability-policy.json")]
    public async Task ValidateAsync_ModifiedModelPolicy_ReturnsDedicatedDiagnostic(string name, string file)
    {
        Directory.CreateDirectory(_root);
        var policyPath = Path.Combine(_root, "model-naming.json");
        await File.WriteAllTextAsync(policyPath,
            await File.ReadAllTextAsync(Policy(file)) + "\n ");
        var request = ModelRequest();
        request = request with
        {
            ModelPolicies = request.ModelPolicies
                .Select(asset => asset.LogicalName == name
                    ? asset with { Path = policyPath }
                    : asset)
                .ToArray()
        };

        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(request);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.IncompatibleModelPolicy &&
            diagnostic.SourceFile == $"<compatibility:model-policy:{name}>");
    }

    [Theory]
    [InlineData("policyVersion", "1.2.0", "primitive-policy-version", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    [InlineData("fhirVersion", "4.0.1", "primitive-policy-fhir-version", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    [InlineData("runtimeContractVersion", "phase-x-v2", "primitive-runtime-contract-version", GeneratorDiagnosticCodes.IncompatibleRuntimeContractVersion)]
    [InlineData("hash", "", "primitive-policy-sha256", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    public async Task ValidateAsync_PrimitivePolicyDimensions_RejectInBothModes(
        string field, string value, string dimension, string code)
    {
        Directory.CreateDirectory(_root);
        var text = await File.ReadAllTextAsync(Policy("primitive-generation-policy.json"));
        if (field != "hash")
        {
            var node = JsonNode.Parse(text)!;
            node[field] = value;
            if (field == "fhirVersion")
            {
                foreach (var primitive in node["primitives"]!.AsArray())
                    primitive!["fhirVersion"] = value;
            }
            text = node.ToJsonString();
        }
        var path = Path.Combine(_root, "changed-policy.json");
        await File.WriteAllTextAsync(path, text + "\n");
        foreach (var primitive in new[] { false, true })
        {
            var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
                .ValidateAsync(Request(primitive) with { PrimitivePolicyPath = path });
            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics, item => item.Code == code &&
                item.SourceFile == $"<compatibility:{dimension}>");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAsync_PolicyLineEndings_DoNotChangeCompatibility(bool primitive)
    {
        Directory.CreateDirectory(_root);
        var text = await File.ReadAllTextAsync(Policy("primitive-generation-policy.json"));
        var path = Path.Combine(_root, "crlf-policy.json");
        await File.WriteAllTextAsync(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        var service = CreateService(CodeGenTestRuntime.RuntimeContract);
        var baseline = await service.ValidateAsync(Request(primitive));
        var actual = await service.ValidateAsync(Request(primitive) with { PrimitivePolicyPath = path });
        Assert.True(actual.IsSuccess, Describe(actual.Diagnostics));
        Assert.Equal(baseline.Value, actual.Value);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAsync_MatchingOldDescriptorAndRequest_CannotBypassCurrentCodeGen(bool primitive)
    {
        var contract = await LoadModifiedContractAsync(root => root["compatibility"]!["codeGenVersion"] = "1.0.0");
        var result = await CreateService(contract).ValidateAsync(Request(primitive) with { CodeGenVersion = "1.0.0" });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == GeneratorDiagnosticCodes.IncompatibleCodeGenVersion &&
            item.SourceFile == "<compatibility:supported-codegen-version>");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAsync_ModelPolicySetMustMatchDescriptor(bool unknownPolicy)
    {
        var request = ModelRequest();
        var policies = request.ModelPolicies.Where(asset => asset.LogicalName != "model-naming").ToList();
        if (unknownPolicy) policies.Add(new("unknown-policy", Policy("r5-model-naming-policy.json")));
        var result = await CreateService(CodeGenTestRuntime.RuntimeContract)
            .ValidateAsync(request with { ModelPolicies = policies });
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == GeneratorDiagnosticCodes.IncompatibleModelPolicy &&
            item.SourceFile == "<compatibility:model-naming>");
        if (unknownPolicy) Assert.Contains(result.Diagnostics, item =>
            item.Code == GeneratorDiagnosticCodes.IncompatibleModelPolicy && item.SourceFile == "<compatibility:unknown-policy>");
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

    private static GenerationCompatibilityRequest Request(bool primitive) => primitive
        ? GenerationCompatibilityRequest.Primitive("1.1.0", "hl7.fhir.r5.core", "5.0.0", "5.0.0",
            Policy("primitive-generation-policy.json"))
        : ModelRequest();

    private static GenerationCompatibilityRequest ModelRequest() =>
        new(
            "1.1.0",
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
