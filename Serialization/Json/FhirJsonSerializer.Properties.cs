using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonSerializer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SerializableProperties = new();

    private static PropertyInfo[] GetSerializableProperties(Type type)
    {
        return SerializableProperties.GetOrAdd(
            type,
            static currentType => currentType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => !ShouldSkipProperty(property))
                .OrderBy(property => GetInheritanceDepth(property.DeclaringType))
                .ThenBy(property => property.MetadataToken)
                .ToArray());
    }

    private static bool ShouldSkipProperty(PropertyInfo property)
    {
        return property.GetMethod is null
            || property.GetMethod.GetParameters().Length > 0
            || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null
            || property.Name == nameof(Resource.ResourceType)
            || property.Name == "HasValue"
            || property.Name == "Literal";
    }

    private static int GetInheritanceDepth(Type? type)
    {
        var depth = 0;

        for (var current = type; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }

    private static string GetJsonPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? ToCamelCase(property.Name);
    }

    private static string GetExtensionValuePropertyName(object value)
    {
        var typeName = value.GetType().Name;
        if (typeName.StartsWith("Fhir", StringComparison.Ordinal))
        {
            typeName = typeName["Fhir".Length..];
        }

        return "value" + typeName;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
