using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Policy;

public sealed class ModelIrGenerationPolicy
{
    internal ModelIrGenerationPolicy(
        string fhirVersion,
        string datatypeNamespace,
        string resourceNamespace,
        string backboneNamespace,
        string backboneBaseClrType,
        string openTypeClrType,
        IEnumerable<ModelIrMemberRename> memberRenames,
        IEnumerable<ModelIrProfileTypeOverride> profileTypeOverrides,
        IEnumerable<ModelIrBackboneRename> backboneRenames,
        IEnumerable<string> openTypeElementIds,
        IEnumerable<string> syntheticResourceMemberNames)
    {
        FhirVersion = fhirVersion;
        DatatypeNamespace = datatypeNamespace;
        ResourceNamespace = resourceNamespace;
        BackboneNamespace = backboneNamespace;
        BackboneBaseClrType = backboneBaseClrType;
        OpenTypeClrType = openTypeClrType;
        MemberRenames = new ReadOnlyDictionary<string, ModelIrMemberRename>(
            memberRenames.ToDictionary(rename => rename.ElementId, StringComparer.Ordinal));
        ProfileTypeOverrides = new ReadOnlyDictionary<string, ModelIrProfileTypeOverride>(
            profileTypeOverrides.ToDictionary(item => item.ProfileCanonical, StringComparer.Ordinal));
        BackboneRenames = new ReadOnlyDictionary<string, ModelIrBackboneRename>(
            backboneRenames.ToDictionary(rename => rename.ElementId, StringComparer.Ordinal));
        OpenTypeElementIds = new HashSet<string>(openTypeElementIds, StringComparer.Ordinal);
        SyntheticResourceMemberNames = new HashSet<string>(
            syntheticResourceMemberNames,
            StringComparer.Ordinal);
    }

    public string FhirVersion { get; }

    public string DatatypeNamespace { get; }

    public string ResourceNamespace { get; }

    public string BackboneNamespace { get; }

    public string BackboneBaseClrType { get; }

    public string OpenTypeClrType { get; }

    public IReadOnlyDictionary<string, ModelIrMemberRename> MemberRenames { get; }

    public IReadOnlyDictionary<string, ModelIrProfileTypeOverride> ProfileTypeOverrides { get; }

    public IReadOnlyDictionary<string, ModelIrBackboneRename> BackboneRenames { get; }

    public IReadOnlySet<string> OpenTypeElementIds { get; }

    public IReadOnlySet<string> SyntheticResourceMemberNames { get; }
}

public sealed record ModelIrMemberRename(
    string ElementId,
    string ClrName,
    string JsonName);

public sealed record ModelIrProfileTypeOverride(
    string ProfileCanonical,
    string ClrType);

public sealed record ModelIrBackboneRename(
    string ElementId,
    string ClrName);
