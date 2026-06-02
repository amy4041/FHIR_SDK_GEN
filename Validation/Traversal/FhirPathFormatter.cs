using System.Reflection;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Validation.Traversal;

internal static class FhirPathFormatter
{
    public static string Root(FhirObject value)
    {
        return value is Resource resource
            ? resource.ResourceType
            : value.GetType().Name;
    }

    public static string Combine(string parentPath, string childName)
    {
        return string.IsNullOrEmpty(parentPath)
            ? childName
            : parentPath + "." + childName;
    }

    public static string Indexed(string path, int index)
    {
        return path + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
    }

    public static string PropertyName(PropertyInfo property)
    {
        var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrWhiteSpace(jsonName))
        {
            return jsonName;
        }

        return ToLowerCamelCase(property.Name);
    }

    private static string ToLowerCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
