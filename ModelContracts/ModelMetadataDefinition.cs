using MyFhirSdk.Core;

namespace MyFhirSdk.ModelContracts;

public sealed record ResourceDefinition(string FhirTypeName, Type ResourceType, Func<Resource> Factory);
public sealed record DeclaredDataTypeDefinition(Type DeclaringType, string PropertyName, Type ConcreteType);
public sealed record ExtensionValueDefinition(Type ValueType, string PropertyName, bool IsParserTarget = true);
public sealed record OpenTypeDefinition(Type DeclaringType, string PropertyName, Type ValueType, string JsonPropertyName);

/// <summary>Declarative metadata and exact allowed CLR identities, copied on construction.</summary>
public sealed class ModelMetadataDefinition
{
    public ModelMetadataDefinition(ModelIdentity identity, IEnumerable<Type> modelTypes,
        IEnumerable<ResourceDefinition> resources, IEnumerable<Type> concreteDataTypes,
        IEnumerable<DeclaredDataTypeDefinition> declaredDataTypes,
        IEnumerable<ExtensionValueDefinition> extensionValues,
        IEnumerable<OpenTypeDefinition>? openTypes = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        ModelTypes = Freeze(modelTypes);
        Resources = Freeze(resources);
        ConcreteDataTypes = Freeze(concreteDataTypes);
        DeclaredDataTypes = Freeze(declaredDataTypes);
        ExtensionValues = Freeze(extensionValues);
        OpenTypes = Freeze(openTypes ?? []);
    }

    public ModelIdentity Identity { get; }
    public IReadOnlyList<Type> ModelTypes { get; }
    public IReadOnlyList<ResourceDefinition> Resources { get; }
    public IReadOnlyList<Type> ConcreteDataTypes { get; }
    public IReadOnlyList<DeclaredDataTypeDefinition> DeclaredDataTypes { get; }
    public IReadOnlyList<ExtensionValueDefinition> ExtensionValues { get; }
    public IReadOnlyList<OpenTypeDefinition> OpenTypes { get; }

    internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var copy = items.ToArray();
        if (copy.Any(item => item is null)) throw new ArgumentException("Null provider entry.", nameof(items));
        return Array.AsReadOnly(copy);
    }
}
