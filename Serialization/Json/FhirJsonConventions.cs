using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

internal static class FhirJsonConventions
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SerializableProperties = new();

    internal static PropertyInfo[] GetSerializableProperties(Type type)
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

    internal static bool ShouldSkipProperty(PropertyInfo property)
    {
        return property.GetMethod is null
            || property.GetMethod.GetParameters().Length > 0
            || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null
            || property.Name == nameof(Resource.ResourceType)
            || property.Name == "HasValue"
            || property.Name == "Literal";
    }

    internal static string GetJsonPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? ToCamelCase(property.Name);
    }

    internal static string GetPrimitiveMetadataPropertyName(string propertyName)
    {
        return "_" + propertyName;
    }

    internal static string GetExtensionValuePropertyName(object value)
    {
        return GetExtensionValuePropertyName(value.GetType());
    }

    internal static string GetExtensionValuePropertyName(Type type)
    {
        return FhirExtensionValuePropertyNames.GetPropertyName(type);
    }

    internal static string GetResourceTypeName(Resource resource)
    {
        return string.IsNullOrWhiteSpace(resource.ResourceType)
            ? resource.GetType().Name
            : resource.ResourceType;
    }

    internal static bool HasPrimitiveRawValue(object primitive)
    {
        if (TryGetDecimalLiteral(primitive, out _) || TryGetInteger64Literal(primitive, out _))
        {
            return true;
        }

        return HasRawJsonValue(GetPrimitiveRawValue(primitive));
    }

    internal static bool TryWritePrimitiveJsonValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing)
    {
        if (TryWriteDecimalJsonValue(writer, primitive, writeNullWhenMissing))
        {
            return true;
        }

        if (TryWriteInteger64JsonValue(writer, primitive, writeNullWhenMissing))
        {
            return true;
        }

        return false;
    }

    internal static bool HasRawJsonValue(object? value)
    {
        return value switch
        {
            null => false,
            string stringValue => stringValue.Length > 0,
            _ => true
        };
    }

    internal static object? GetPrimitiveRawValue(object primitive)
    {
        return GetPrimitiveValueProperty(primitive.GetType())?.GetValue(primitive);
    }

    internal static PropertyInfo? GetPrimitiveValueProperty(Type primitiveType)
    {
        return primitiveType.GetProperty(nameof(PrimitiveType<object>.Value));
    }

    internal static bool TryGetDecimalLiteral(object primitive, out string decimalLiteral)
    {
        decimalLiteral = string.Empty;

        if (primitive.GetType().Name != "FhirDecimal")
        {
            return false;
        }

        var literal = GetLiteralPropertyValue(primitive);
        if (string.IsNullOrEmpty(literal))
        {
            return false;
        }

        decimalLiteral = literal;
        return true;
    }

    internal static bool TryGetInteger64Literal(object primitive, out string integer64Literal)
    {
        integer64Literal = string.Empty;

        if (primitive.GetType().Name != "FhirInteger64")
        {
            return false;
        }

        var literal = GetLiteralPropertyValue(primitive);
        if (string.IsNullOrEmpty(literal))
        {
            return false;
        }

        integer64Literal = literal;
        return true;
    }

    private static bool TryWriteDecimalJsonValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing)
    {
        if (primitive.GetType().Name != "FhirDecimal")
        {
            return false;
        }

        if (TryGetDecimalLiteral(primitive, out var literal))
        {
            writer.WriteRawValue(literal);
            return true;
        }

        if (GetPrimitiveRawValue(primitive) is decimal decimalValue)
        {
            writer.WriteRawValue(decimalValue.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        if (writeNullWhenMissing)
        {
            writer.WriteNullValue();
        }

        return false;
    }

    private static bool TryWriteInteger64JsonValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing)
    {
        if (primitive.GetType().Name != "FhirInteger64")
        {
            return false;
        }

        if (TryGetInteger64Literal(primitive, out var literal))
        {
            writer.WriteStringValue(literal);
            return true;
        }

        if (GetPrimitiveRawValue(primitive) is long longValue)
        {
            writer.WriteStringValue(longValue.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        if (writeNullWhenMissing)
        {
            writer.WriteNullValue();
        }

        return false;
    }

    private static string? GetLiteralPropertyValue(object primitive)
    {
        return primitive.GetType()
            .GetProperty("Literal", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(primitive) as string;
    }

    internal static bool IsFhirPrimitive(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(PrimitiveType<>))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsSimpleValue(object value)
    {
        return IsSimpleValueType(value.GetType());
    }

    internal static bool IsSimpleValueType(Type type)
    {
        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;

        return nonNullableType == typeof(bool)
            || nonNullableType == typeof(int)
            || nonNullableType == typeof(long)
            || nonNullableType == typeof(decimal)
            || nonNullableType == typeof(DateTimeOffset)
            || nonNullableType == typeof(DateTime);
    }

    internal static string FormatDateTimeOffset(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    internal static string FormatDateTime(DateTime value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    internal static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
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
}
