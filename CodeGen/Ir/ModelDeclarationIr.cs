using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelDeclarationIr
{
    internal ModelDeclarationIr(
        ModelIrSource source,
        ModelIrCategory category,
        string fhirName,
        string cSharpName,
        string @namespace,
        string artifactPath,
        bool isAbstract,
        bool isSealed,
        ModelTypeReferenceIr baseType,
        string? resourceOwnerCanonical,
        string? backboneElementId,
        IEnumerable<ModelMemberIr> members)
    {
        Source = source;
        Category = category;
        FhirName = fhirName;
        CSharpName = cSharpName;
        Namespace = @namespace;
        ArtifactPath = artifactPath;
        IsAbstract = isAbstract;
        IsSealed = isSealed;
        BaseType = baseType;
        ResourceOwnerCanonical = resourceOwnerCanonical;
        BackboneElementId = backboneElementId;
        Members = new ReadOnlyCollection<ModelMemberIr>(members.ToArray());
    }

    public ModelIrSource Source { get; }

    public ModelIrCategory Category { get; }

    public string FhirName { get; }

    public string CSharpName { get; }

    public string Namespace { get; }

    public string ArtifactPath { get; }

    public bool IsAbstract { get; }

    public bool IsSealed { get; }

    public ModelTypeReferenceIr BaseType { get; }

    public string? ResourceOwnerCanonical { get; }

    public string? BackboneElementId { get; }

    public IReadOnlyList<ModelMemberIr> Members { get; }

    public string FullyQualifiedName => $"{Namespace}.{CSharpName}";
}
