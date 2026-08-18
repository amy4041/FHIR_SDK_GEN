using System.Text.Json;

namespace MyFhirSdk.Primitives;

internal interface IPrimitiveDefinition
{
    string FhirTypeName { get; }

    Type PrimitiveType { get; }

    Type ValueType { get; }

    IPrimitiveCodec Codec { get; }

    IPrimitiveValidator Validator { get; }
}

internal interface IPrimitiveCodec
{
    object CreatePrimitive(Type primitiveType, JsonElement? rawElement);

    bool HasRawValue(object primitive);

    void WriteRawValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing);
}

internal interface IPrimitiveValidator
{
    bool IsValid(object primitive);

    bool IsValidValue(object? value);
}
