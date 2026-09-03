using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Metadata;

public sealed class ModelMetadataIrBatch
{
    internal ModelMetadataIrBatch(
        IEnumerable<ResourceFactoryMetadataIr> resources,
        IEnumerable<ConcreteDatatypeMetadataIr> concreteDatatypes,
        IEnumerable<DeclaredDatatypeMetadataIr> declaredDatatypes,
        IEnumerable<ExtensionValueMetadataIr> extensionValues,
        IEnumerable<OpenTypeMetadataIr> openTypes,
        IEnumerable<ValidationTypeMetadataIr> validationTypes)
    {
        Resources = new ReadOnlyCollection<ResourceFactoryMetadataIr>(resources.ToArray());
        ConcreteDatatypes = new ReadOnlyCollection<ConcreteDatatypeMetadataIr>(
            concreteDatatypes.ToArray());
        DeclaredDatatypes = new ReadOnlyCollection<DeclaredDatatypeMetadataIr>(
            declaredDatatypes.ToArray());
        ExtensionValues = new ReadOnlyCollection<ExtensionValueMetadataIr>(extensionValues.ToArray());
        OpenTypes = new ReadOnlyCollection<OpenTypeMetadataIr>(openTypes.ToArray());
        ValidationTypes = new ReadOnlyCollection<ValidationTypeMetadataIr>(validationTypes.ToArray());
    }

    public IReadOnlyList<ResourceFactoryMetadataIr> Resources { get; }

    public IReadOnlyList<ConcreteDatatypeMetadataIr> ConcreteDatatypes { get; }

    public IReadOnlyList<DeclaredDatatypeMetadataIr> DeclaredDatatypes { get; }

    public IReadOnlyList<ExtensionValueMetadataIr> ExtensionValues { get; }

    public IReadOnlyList<OpenTypeMetadataIr> OpenTypes { get; }

    public IReadOnlyList<ValidationTypeMetadataIr> ValidationTypes { get; }
}

public sealed record ResourceFactoryMetadataIr(
    string FhirTypeName,
    string ClrType);

public sealed record ConcreteDatatypeMetadataIr(string ClrType);

public sealed record DeclaredDatatypeMetadataIr(
    string DeclaringClrType,
    string PropertyName,
    string ConcreteClrType);

public sealed record ExtensionValueMetadataIr(
    string FhirTypeCode,
    string ClrType,
    string JsonPropertyName);

public sealed record OpenTypeMetadataIr(
    string DeclaringClrType,
    string ClrPropertyName,
    string ChoiceElementId,
    string FhirTypeCode,
    string ValueClrType,
    string JsonPropertyName);

public sealed class ValidationTypeMetadataIr
{
    internal ValidationTypeMetadataIr(
        string clrType,
        IEnumerable<ValidationRuleMetadataIr> rules)
    {
        ClrType = clrType;
        Rules = new ReadOnlyCollection<ValidationRuleMetadataIr>(rules.ToArray());
    }

    public string ClrType { get; }

    public IReadOnlyList<ValidationRuleMetadataIr> Rules { get; }
}

public sealed record ValidationRuleMetadataIr(
    ValidationRuleKind Kind,
    string ElementId,
    string FhirPath,
    IReadOnlyList<string> ClrPropertyNames);

public enum ValidationRuleKind
{
    RequiredScalar,
    RequiredCollection,
    ChoiceAtMostOne,
    ChoiceExactlyOne
}
