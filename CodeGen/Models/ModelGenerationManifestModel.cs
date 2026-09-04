using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Models;

public sealed class ModelGenerationManifestModel
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "Generated/R5/model-generation-manifest.json";

    public ModelGenerationManifestModel(
        string packageId,
        string packageVersion,
        string fhirVersion,
        string packageSha256,
        string primitivePolicyVersion,
        string primitivePolicySha256,
        string codeGenVersion,
        string runtimeContractVersion,
        string scope,
        IEnumerable<string> selectedCanonicals,
        IEnumerable<ModelManifestPolicyModel> modelPolicies,
        IEnumerable<ModelManifestArtifactModel> artifacts,
        IEnumerable<ModelManifestCapabilityModel> deferredCapabilities)
    {
        PackageId = packageId;
        PackageVersion = packageVersion;
        FhirVersion = fhirVersion;
        PackageSha256 = packageSha256;
        PrimitivePolicyVersion = primitivePolicyVersion;
        PrimitivePolicySha256 = primitivePolicySha256;
        CodeGenVersion = codeGenVersion;
        RuntimeContractVersion = runtimeContractVersion;
        Scope = scope;
        SelectedCanonicals = Array.AsReadOnly(selectedCanonicals.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        ModelPolicies = new ReadOnlyCollection<ModelManifestPolicyModel>(modelPolicies.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray());
        Artifacts = new ReadOnlyCollection<ModelManifestArtifactModel>(artifacts.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray());
        DeferredCapabilities = new ReadOnlyCollection<ModelManifestCapabilityModel>(deferredCapabilities.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray());
    }

    public int SchemaVersion => CurrentSchemaVersion;
    public string PackageId { get; }
    public string PackageVersion { get; }
    public string FhirVersion { get; }
    public string PackageSha256 { get; }
    public string PrimitivePolicyVersion { get; }
    public string PrimitivePolicySha256 { get; }
    public string CodeGenVersion { get; }
    public string RuntimeContractVersion { get; }
    public string Scope { get; }
    public IReadOnlyList<string> SelectedCanonicals { get; }
    public IReadOnlyList<ModelManifestPolicyModel> ModelPolicies { get; }
    public IReadOnlyList<ModelManifestArtifactModel> Artifacts { get; }
    public IReadOnlyList<ModelManifestCapabilityModel> DeferredCapabilities { get; }
}

public sealed record ModelManifestPolicyModel(string Name, string Sha256);
public sealed record ModelManifestArtifactModel(string Path, string Sha256);
public sealed record ModelManifestCapabilityModel(string Id, string Status);
