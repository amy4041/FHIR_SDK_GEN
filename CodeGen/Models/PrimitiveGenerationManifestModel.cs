using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Models;

public sealed class PrimitiveGenerationManifestModel
{
    public const int CurrentSchemaVersion = 1;

    public PrimitiveGenerationManifestModel(
        string fhirSpecification,
        string fhirPackageId,
        string fhirPackageVersion,
        string fhirVersion,
        string policyVersion,
        string codeGenVersion,
        string runtimeContractVersion,
        string primitiveNamespace,
        IEnumerable<PrimitiveManifestDecisionModel> primitives,
        IEnumerable<PrimitiveManifestArtifactModel> artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirSpecification);
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirPackageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirPackageVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeGenVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeContractVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(primitiveNamespace);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(artifacts);

        FhirSpecification = fhirSpecification;
        FhirPackageId = fhirPackageId;
        FhirPackageVersion = fhirPackageVersion;
        FhirVersion = fhirVersion;
        PolicyVersion = policyVersion;
        CodeGenVersion = codeGenVersion;
        RuntimeContractVersion = runtimeContractVersion;
        PrimitiveNamespace = primitiveNamespace;
        Primitives = new ReadOnlyCollection<PrimitiveManifestDecisionModel>(
            primitives.OrderBy(item => item.FhirTypeName, StringComparer.Ordinal).ToArray());
        Artifacts = new ReadOnlyCollection<PrimitiveManifestArtifactModel>(
            artifacts.OrderBy(item => item.FileName, StringComparer.Ordinal).ToArray());
    }

    public int SchemaVersion => CurrentSchemaVersion;
    public string FhirSpecification { get; }
    public string FhirPackageId { get; }
    public string FhirPackageVersion { get; }
    public string FhirVersion { get; }
    public string PolicyVersion { get; }
    public string CodeGenVersion { get; }
    public string RuntimeContractVersion { get; }
    public string PrimitiveNamespace { get; }
    public IReadOnlyList<PrimitiveManifestDecisionModel> Primitives { get; }
    public IReadOnlyList<PrimitiveManifestArtifactModel> Artifacts { get; }
    public string FileName => "primitive-generation-manifest.json";
}

public sealed record PrimitiveManifestDecisionModel(
    string FhirTypeName,
    string Canonical,
    string FhirVersion,
    string SupportStatus,
    string? UnsupportedReason,
    string? WrapperName);

public sealed record PrimitiveManifestArtifactModel(
    string FileName,
    string Sha256);
