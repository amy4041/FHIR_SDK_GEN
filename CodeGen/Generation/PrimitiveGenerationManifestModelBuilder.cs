using System.Security.Cryptography;
using System.Text;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class PrimitiveGenerationManifestModelBuilder
{
    public PrimitiveGenerationManifestModel Build(
        PrimitiveInventoryPolicyCoverage coverage,
        PrimitiveGenerationOptions options,
        IReadOnlyList<GeneratedSource> sources)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sources);

        var decisions = coverage.Matches.Select(match =>
            new PrimitiveManifestDecisionModel(
                match.Definition.FhirTypeName,
                match.Definition.Canonical,
                match.Definition.FhirVersion,
                match.Policy.IsSupported ? "supported" : "unsupported",
                match.Policy.UnsupportedReason,
                match.Policy.WrapperName));
        var artifacts = sources.Select(source =>
            new PrimitiveManifestArtifactModel(
                source.FileName,
                Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(NormalizeNewlines(source.Source))))
                    .ToLowerInvariant()));

        return new PrimitiveGenerationManifestModel(
            options.FhirSpecification,
            options.FhirPackageId,
            options.FhirPackageVersion,
            options.FhirVersion,
            coverage.Policy.PolicyVersion,
            options.CodeGenVersion,
            coverage.Policy.RuntimeContractVersion,
            coverage.Policy.PrimitiveNamespace,
            decisions,
            artifacts);
    }

    private static string NormalizeNewlines(string value) => value
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n');
}
