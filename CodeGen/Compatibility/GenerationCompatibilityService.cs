using System.Security;
using System.Security.Cryptography;
using System.Text;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Compatibility;

public sealed class GenerationCompatibilityService
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly RuntimeContractView _contract;
    private readonly RuntimeReferenceSet _references;
    private readonly PrimitiveGenerationPolicyLoader _primitivePolicyLoader = new();
    private readonly PrimitiveGenerationPolicyValidator _primitivePolicyValidator = new();

    public GenerationCompatibilityService(
        RuntimeContractView contract,
        RuntimeReferenceSet references)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(references);
        _contract = contract;
        _references = references;
    }

    public async Task<GenerationResult<GenerationManifestProvenance?>> ValidateAsync(
        GenerationCompatibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var assetDiagnostics = new List<GeneratorDiagnostic>();
        var primitiveHash = await TryHashTextAssetAsync(
            request.PrimitivePolicyPath,
            "primitive-policy",
            assetDiagnostics,
            cancellationToken);
        if (primitiveHash is null)
        {
            return Failure(assetDiagnostics);
        }

        var policyLoad = await _primitivePolicyLoader.LoadAsync(
            request.PrimitivePolicyPath,
            cancellationToken);
        if (!policyLoad.IsSuccess || policyLoad.Value is null)
        {
            return Failure([
                AssetDiagnostic(
                    GeneratorDiagnosticCodes.PackagedAssetCorrupt,
                    "primitive-policy",
                    "is unreadable, corrupt, or not valid JSON")
            ]);
        }

        var policyResult = _primitivePolicyValidator.Validate(
            policyLoad.Value,
            Path.GetFullPath(request.PrimitivePolicyPath));
        if (!policyResult.IsSuccess || policyResult.Value is null)
        {
            return Failure(policyResult.Diagnostics);
        }

        var diagnostics = new List<GeneratorDiagnostic>();
        var compatibility = _contract.Compatibility;
        Compare(
            "compatibility-schema-version",
            compatibility.SchemaVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            GenerationCompatibilityMatrix.SchemaVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            GeneratorDiagnosticCodes.IncompatibleCompatibilitySchema,
            diagnostics);
        Compare(
            "compatibility-version-policy",
            compatibility.VersionPolicy,
            GenerationCompatibilityMatrix.VersionPolicy,
            GeneratorDiagnosticCodes.IncompatibleCompatibilitySchema,
            diagnostics);
        Compare(
            "tool-version",
            GenerationCompatibilityMatrix.ToolVersion,
            compatibility.ToolVersion,
            GeneratorDiagnosticCodes.IncompatibleToolVersion,
            diagnostics);
        Compare(
            "codegen-version",
            request.CodeGenVersion,
            compatibility.CodeGenVersion,
            GeneratorDiagnosticCodes.IncompatibleCodeGenVersion,
            diagnostics);
        Compare(
            "supported-codegen-version",
            request.CodeGenVersion,
            GenerationCompatibilityMatrix.CodeGenVersion,
            GeneratorDiagnosticCodes.IncompatibleCodeGenVersion,
            diagnostics);
        Compare(
            "runtime-contract-version",
            _contract.ContractVersion,
            GenerationCompatibilityMatrix.RuntimeContractVersion,
            GeneratorDiagnosticCodes.IncompatibleRuntimeContractVersion,
            diagnostics);
        Compare(
            "runtime-reference-contract-sha256",
            _references.ContractSha256,
            _contract.DescriptorSha256,
            GeneratorDiagnosticCodes.RuntimeContractReferenceMismatch,
            diagnostics);
        Compare(
            "primitive-runtime-contract-version",
            policyResult.Value.RuntimeContractVersion,
            _contract.ContractVersion,
            GeneratorDiagnosticCodes.IncompatibleRuntimeContractVersion,
            diagnostics);
        Compare(
            "target-framework",
            _contract.TargetFramework,
            GenerationCompatibilityMatrix.TargetFramework,
            GeneratorDiagnosticCodes.UnsupportedTargetFramework,
            diagnostics);
        Compare(
            "compiler-reference-target-framework",
            _references.TargetFramework,
            _contract.TargetFramework,
            GeneratorDiagnosticCodes.UnsupportedTargetFramework,
            diagnostics);

        Compare(
            "fhir-package-id",
            request.FhirPackageId,
            compatibility.FhirPackage.Id,
            GeneratorDiagnosticCodes.IncompatibleFhirPackage,
            diagnostics);
        Compare(
            "fhir-package-version",
            request.FhirPackageVersion,
            compatibility.FhirPackage.Version,
            GeneratorDiagnosticCodes.IncompatibleFhirPackage,
            diagnostics);
        Compare(
            "fhir-version",
            request.FhirVersion,
            compatibility.FhirPackage.FhirVersion,
            GeneratorDiagnosticCodes.IncompatibleFhirPackage,
            diagnostics);
        Compare(
            "primitive-policy-fhir-version",
            policyResult.Value.FhirVersion,
            request.FhirVersion,
            GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy,
            diagnostics);
        Compare(
            "primitive-policy-version",
            policyResult.Value.PolicyVersion,
            compatibility.PrimitivePolicy.Version,
            GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy,
            diagnostics);

        Compare(
            "primitive-policy-sha256",
            primitiveHash,
            compatibility.PrimitivePolicy.Sha256,
            GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy,
            diagnostics);

        var expectedModelPolicies = compatibility.ModelPolicies.ToDictionary(
            asset => asset.Name,
            StringComparer.Ordinal);
        foreach (var asset in request.ModelPolicies
                     .OrderBy(asset => asset.LogicalName, StringComparer.Ordinal))
        {
            if (!expectedModelPolicies.TryGetValue(asset.LogicalName, out var expected))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.IncompatibleModelPolicy,
                    asset.LogicalName,
                    "present",
                    "not declared"));
                continue;
            }

            var actualHash = await TryHashTextAssetAsync(
                asset.Path,
                $"model-policy:{asset.LogicalName}",
                diagnostics,
                cancellationToken);
            if (actualHash is not null)
            {
                Compare(
                    $"model-policy:{asset.LogicalName}",
                    actualHash,
                    expected.Sha256,
                    GeneratorDiagnosticCodes.IncompatibleModelPolicy,
                    diagnostics);
            }
        }

        if (request.ModelPolicies.Count > 0)
        {
            var actualNames = request.ModelPolicies
                .Select(asset => asset.LogicalName)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var missing in expectedModelPolicies.Keys
                         .Where(name => !actualNames.Contains(name))
                         .OrderBy(name => name, StringComparer.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.IncompatibleModelPolicy,
                    missing,
                    "missing",
                    "present"));
            }
        }

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToArray();
        if (orderedDiagnostics.Length > 0)
        {
            return Failure(orderedDiagnostics);
        }

        return new GenerationResult<GenerationManifestProvenance?>(
            new GenerationManifestProvenance(
                GenerationCompatibilityMatrix.SchemaVersion,
                GenerationCompatibilityMatrix.VersionPolicy,
                GenerationCompatibilityMatrix.ToolPackageId,
                GenerationCompatibilityMatrix.ToolVersion,
                request.CodeGenVersion,
                _contract.SchemaVersion,
                _contract.ContractVersion,
                _contract.DescriptorSha256,
                _references.LogicalAssemblyIdentity.ToString(),
                _references.ReferenceSha256,
                _references.TargetFramework),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static async Task<string?> TryHashTextAssetAsync(
        string path,
        string logicalName,
        ICollection<GeneratorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.PackagedAssetMissing,
                GeneratorDiagnosticSeverity.Error,
                $"Required asset '{logicalName}' is missing.",
                $"<asset:{logicalName}>"));
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var text = StrictUtf8.GetString(bytes);
            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            return Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(normalized)))
                .ToLowerInvariant();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException or SecurityException or
            ArgumentException or NotSupportedException)
        {
            diagnostics.Add(AssetDiagnostic(
                GeneratorDiagnosticCodes.PackagedAssetCorrupt,
                logicalName,
                "is unreadable or corrupt"));
            return null;
        }
    }

    private static void Compare(
        string dimension,
        string actual,
        string expected,
        string code,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic(code, dimension, actual, expected));
        }
    }

    private static GeneratorDiagnostic Diagnostic(
        string code,
        string dimension,
        string actual,
        string expected) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            $"Compatibility dimension '{dimension}' has actual value '{actual}'; expected '{expected}'.",
            $"<compatibility:{dimension}>");

    private static GeneratorDiagnostic AssetDiagnostic(
        string code,
        string logicalName,
        string failure) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            $"Required asset '{logicalName}' {failure}.",
            $"<asset:{logicalName}>");

    private static GenerationResult<GenerationManifestProvenance?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
}
