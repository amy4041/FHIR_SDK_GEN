using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;

namespace MyFhirSdk.ModelMetadata;

// Assembly-local bindings preserve the existing generated format. They only
// describe models; all runtime lookup and execution remain outside Models.
internal sealed record ResourceTypeMetadata(string Name, Type Type, Func<Resource> Factory);
internal sealed record DeclaredDataTypeMetadata(Type DeclaringType, string PropertyName, Type ConcreteType);
internal sealed record ExtensionValueMetadata(Type ValueType, string PropertyName, bool IsParserTarget = true);
internal sealed record OpenTypeValueMetadata(Type DeclaringType, string PropertyName, Type ValueType, string JsonPropertyName);

internal sealed class ImmutableModelMetadataProvider
{
    internal ImmutableModelMetadataProvider(IEnumerable<ResourceTypeMetadata> resources, IEnumerable<Type> dataTypes,
        IEnumerable<DeclaredDataTypeMetadata> declared, IEnumerable<ExtensionValueMetadata> extensions,
        IEnumerable<OpenTypeValueMetadata>? openTypes = null)
    {
        Resources = resources.ToArray(); DataTypes = dataTypes.ToArray(); Declared = declared.ToArray();
        Extensions = extensions.ToArray(); OpenTypes = (openTypes ?? []).ToArray();
    }
    internal ResourceTypeMetadata[] Resources { get; }
    internal Type[] DataTypes { get; }
    internal DeclaredDataTypeMetadata[] Declared { get; }
    internal ExtensionValueMetadata[] Extensions { get; }
    internal OpenTypeValueMetadata[] OpenTypes { get; }
    internal ModelMetadataDefinition Export(ModelIdentity identity, IEnumerable<Type> types) => new(identity, types,
        Resources.Select(r => new ResourceDefinition(r.Name, r.Type, r.Factory)), DataTypes,
        Declared.Select(d => new DeclaredDataTypeDefinition(d.DeclaringType, d.PropertyName, d.ConcreteType)),
        Extensions.Select(e => new ExtensionValueDefinition(e.ValueType, e.PropertyName, e.IsParserTarget)),
        OpenTypes.Select(o => new OpenTypeDefinition(o.DeclaringType, o.PropertyName, o.ValueType, o.JsonPropertyName)));
}
