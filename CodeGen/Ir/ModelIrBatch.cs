using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelIrBatch
{
    internal ModelIrBatch(
        IEnumerable<ModelDeclarationIr> declarations,
        IEnumerable<ExternalModelMetadataIr> externalMetadata)
    {
        Declarations = new ReadOnlyCollection<ModelDeclarationIr>(declarations.ToArray());
        ExternalMetadata = new ReadOnlyCollection<ExternalModelMetadataIr>(externalMetadata.ToArray());
    }

    public IReadOnlyList<ModelDeclarationIr> Declarations { get; }

    public IReadOnlyList<ExternalModelMetadataIr> ExternalMetadata { get; }
}

public sealed class ExternalModelMetadataIr
{
    internal ExternalModelMetadataIr(
        ModelIrSource source,
        string fhirName,
        string clrType,
        string kind,
        bool isAbstract,
        IEnumerable<ExternalModelMemberMetadataIr> members)
    {
        Source = source;
        FhirName = fhirName;
        ClrType = clrType;
        Kind = kind;
        IsAbstract = isAbstract;
        Members = new ReadOnlyCollection<ExternalModelMemberMetadataIr>(members.ToArray());
    }

    public ModelIrSource Source { get; }

    public string FhirName { get; }

    public string ClrType { get; }

    public string Kind { get; }

    public bool IsAbstract { get; }

    public IReadOnlyList<ExternalModelMemberMetadataIr> Members { get; }
}

public sealed record ExternalModelMemberMetadataIr(
    ModelIrSource Source,
    string FhirName,
    string JsonName,
    string ClrPropertyName,
    ModelMemberRepresentation Representation,
    CardinalityModel Cardinality,
    IReadOnlyList<ModelTypeReferenceIr> TypeAlternatives);
