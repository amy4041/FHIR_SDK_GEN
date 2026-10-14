using System.Text.Json;

namespace MyFhirSdk.ModelContracts;

/// <summary>Implementations must be stateless or safe for concurrent context use.</summary>
public interface IPrimitiveCodec
{
    object CreatePrimitive(Type primitiveType, JsonElement? rawElement);
    bool HasRawValue(object primitive);
    void WriteRawValue(Utf8JsonWriter writer, object primitive, bool writeNullWhenMissing);
}

public interface IPrimitiveValidator
{
    bool IsValid(object primitive);
    bool IsValidValue(object? value);
}

public sealed record PrimitiveDefinition(string FhirTypeName, Type PrimitiveType, Type ValueType,
    IPrimitiveCodec Codec, IPrimitiveValidator Validator);

public sealed class PrimitiveProviderDefinition
{
    public PrimitiveProviderDefinition(ModelIdentity identity, IEnumerable<PrimitiveDefinition> definitions)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Definitions = ModelMetadataDefinition.Freeze(definitions);
    }

    public ModelIdentity Identity { get; }
    public IReadOnlyList<PrimitiveDefinition> Definitions { get; }
}
