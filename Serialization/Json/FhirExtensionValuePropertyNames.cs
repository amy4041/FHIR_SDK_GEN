using MyFhirSdk.Types;

namespace MyFhirSdk.Serialization.Json;

/// <summary>
/// Maps SDK types to FHIR R5 Extension.value[x] JSON property names.
/// </summary>
internal static class FhirExtensionValuePropertyNames
{
    private static readonly Dictionary<Type, string> Overrides = new()
    {
        [typeof(SimpleQuantity)] = "valueQuantity",
    };

    internal static string GetPropertyName(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (Overrides.TryGetValue(current, out var propertyName))
            {
                return propertyName;
            }
        }

        var typeName = type.Name;
        if (typeName.StartsWith("Fhir", StringComparison.Ordinal))
        {
            typeName = typeName["Fhir".Length..];
        }

        return "value" + typeName;
    }
}
