using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Serialization.Json;

/// <summary>
/// Maps SDK types to FHIR R5 Extension.value[x] JSON property names.
/// </summary>
internal static class FhirExtensionValuePropertyNames
{
    private static readonly Dictionary<Type, string> SerializerOverrides = new()
    {
        [typeof(SimpleQuantity)] = "valueQuantity",
    };

    private static readonly IReadOnlyDictionary<string, Type> ParserExtensionValueTypes =
        BuildParserExtensionValueTypes();

    internal static string GetPropertyName(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SerializerOverrides.TryGetValue(current, out var propertyName))
            {
                return propertyName;
            }
        }

        return GetDefaultPropertyName(type);
    }

    internal static bool TryGetParserExtensionValueType(string propertyName, out Type valueType)
    {
        return ParserExtensionValueTypes.TryGetValue(propertyName, out valueType!);
    }

    private static IReadOnlyDictionary<string, Type> BuildParserExtensionValueTypes()
    {
        var valueTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in typeof(SimpleQuantity).Assembly.GetTypes())
        {
            if (!typeof(IFhirExtensionValue).IsAssignableFrom(type) ||
                !IsConcreteType(type) ||
                UsesSerializerOnlyExtensionPropertyName(type))
            {
                continue;
            }

            valueTypes.TryAdd(GetPropertyName(type), type);
        }

        return valueTypes;
    }

    /// <summary>
    /// Types such as <see cref="SimpleQuantity"/> serialize as <c>valueQuantity</c> but parse as <see cref="Quantity"/>.
    /// </summary>
    private static bool UsesSerializerOnlyExtensionPropertyName(Type type)
    {
        if (!SerializerOverrides.TryGetValue(type, out var propertyName))
        {
            return false;
        }

        return !string.Equals(propertyName, GetDefaultPropertyName(type), StringComparison.Ordinal);
    }

    private static string GetDefaultPropertyName(Type type)
    {
        var typeName = type.Name;
        if (typeName.StartsWith("Fhir", StringComparison.Ordinal))
        {
            typeName = typeName["Fhir".Length..];
        }

        return "value" + typeName;
    }

    private static bool IsConcreteType(Type type)
    {
        return !type.IsAbstract &&
            !type.IsInterface &&
            type.GetConstructor(Type.EmptyTypes) is not null;
    }
}
