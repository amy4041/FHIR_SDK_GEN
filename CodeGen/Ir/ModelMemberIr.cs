using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelMemberIr
{
    internal ModelMemberIr(
        ModelIrSource source,
        string fhirName,
        string jsonName,
        ModelMemberRepresentation representation,
        CardinalityModel cardinality,
        string? choiceStem,
        string? contentReference,
        ModelIrSource? resolvedContentTarget,
        IEnumerable<ModelTypeReferenceIr> typeAlternatives,
        IEnumerable<ModelPropertyIr> properties,
        ModelValidationMetadataIr validation,
        string? documentation,
        int order)
    {
        Source = source;
        FhirName = fhirName;
        JsonName = jsonName;
        Representation = representation;
        Cardinality = cardinality;
        ChoiceStem = choiceStem;
        ContentReference = contentReference;
        ResolvedContentTarget = resolvedContentTarget;
        TypeAlternatives = new ReadOnlyCollection<ModelTypeReferenceIr>(typeAlternatives.ToArray());
        Properties = new ReadOnlyCollection<ModelPropertyIr>(properties.ToArray());
        Validation = validation;
        Documentation = documentation;
        Order = order;
    }

    public ModelIrSource Source { get; }

    public string FhirName { get; }

    public string JsonName { get; }

    public ModelMemberRepresentation Representation { get; }

    public CardinalityModel Cardinality { get; }

    public string? ChoiceStem { get; }

    public string? ContentReference { get; }

    public ModelIrSource? ResolvedContentTarget { get; }

    public IReadOnlyList<ModelTypeReferenceIr> TypeAlternatives { get; }

    public IReadOnlyList<ModelPropertyIr> Properties { get; }

    public ModelValidationMetadataIr Validation { get; }

    public string? Documentation { get; }

    public int Order { get; }
}
