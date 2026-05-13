using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

/// <summary>
/// Serializes typed SDK resources to FHIR JSON.
/// </summary>
public sealed class FhirJsonSerializer : IFhirSerializer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SerializableProperties = new();

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false
    };

    public string Serialize<TResource>(TResource resource)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(resource);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, WriterOptions);

        WriteResourceValue(writer, resource);
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteResourceValue(Utf8JsonWriter writer, Resource resource)
    {
        writer.WriteStartObject();
        writer.WriteString("resourceType", GetResourceTypeName(resource));
        WriteObjectProperties(writer, resource);
        writer.WriteEndObject();
    }

    private static string GetResourceTypeName(Resource resource)
    {
        return string.IsNullOrWhiteSpace(resource.ResourceType)
            ? resource.GetType().Name
            : resource.ResourceType;
    }

    private static void WriteObjectValue(Utf8JsonWriter writer, object value)
    {
        if (value is Resource resource)
        {
            WriteResourceValue(writer, resource);
            return;
        }

        writer.WriteStartObject();
        WriteObjectProperties(writer, value);
        writer.WriteEndObject();
    }

    private static void WriteObjectProperties(Utf8JsonWriter writer, object value)
    {
        foreach (var property in GetSerializableProperties(value.GetType()))
        {
            var propertyValue = property.GetValue(value);

            if (property.DeclaringType == typeof(Extension) &&
                property.Name == nameof(Extension.Value))
            {
                TryWriteExtensionValueProperty(writer, propertyValue);
                continue;
            }

            TryWriteProperty(writer, GetJsonPropertyName(property), propertyValue);
        }
    }

    private static bool TryWriteExtensionValueProperty(Utf8JsonWriter writer, object? value)
    {
        if (!HasSerializableValue(value))
        {
            return false;
        }

        return TryWriteProperty(writer, GetExtensionValuePropertyName(value!), value);
    }

    private static bool TryWriteProperty(Utf8JsonWriter writer, string propertyName, object? value)
    {
        if (!HasSerializableValue(value))
        {
            return false;
        }

        var nonNullValue = value!;

        if (nonNullValue is string stringValue)
        {
            writer.WriteString(propertyName, stringValue);
            return true;
        }

        if (TryWriteSimpleProperty(writer, propertyName, nonNullValue))
        {
            return true;
        }

        if (IsFhirPrimitive(nonNullValue.GetType()))
        {
            return WritePrimitiveProperty(writer, propertyName, nonNullValue);
        }

        if (nonNullValue is IEnumerable enumerable)
        {
            return WriteArrayProperty(writer, propertyName, enumerable);
        }

        writer.WritePropertyName(propertyName);
        WriteObjectValue(writer, nonNullValue);
        return true;
    }

    private static bool WriteArrayProperty(Utf8JsonWriter writer, string propertyName, IEnumerable enumerable)
    {
        var items = GetSerializableItems(enumerable);
        if (items.Count == 0)
        {
            return false;
        }

        if (items.TrueForAll(item => item is not null && IsFhirPrimitive(item.GetType())))
        {
            return WritePrimitiveArrayProperty(writer, propertyName, items);
        }

        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var item in items)
        {
            WriteValue(writer, item!);
        }

        writer.WriteEndArray();
        return true;
    }

    private static bool WritePrimitiveProperty(Utf8JsonWriter writer, string propertyName, object primitive)
    {
        var hasRawValue = HasPrimitiveRawValue(primitive);
        var hasMetadata = HasPrimitiveMetadata(primitive);

        if (!hasRawValue && !hasMetadata)
        {
            return false;
        }

        if (hasRawValue)
        {
            writer.WritePropertyName(propertyName);
            WritePrimitiveRawValue(writer, primitive, writeNullWhenMissing: false);
        }

        if (hasMetadata)
        {
            writer.WritePropertyName("_" + propertyName);
            WritePrimitiveMetadataObject(writer, (Element)primitive);
        }

        return true;
    }

    private static bool WritePrimitiveArrayProperty(
        Utf8JsonWriter writer,
        string propertyName,
        List<object?> items)
    {
        var hasAnyRawValue = items.Any(item => item is not null && HasPrimitiveRawValue(item));
        var hasAnyMetadata = items.Any(item => item is not null && HasPrimitiveMetadata(item));

        if (!hasAnyRawValue && !hasAnyMetadata)
        {
            return false;
        }

        if (hasAnyRawValue)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartArray();

            foreach (var item in items)
            {
                WritePrimitiveRawValue(writer, item!, writeNullWhenMissing: true);
            }

            writer.WriteEndArray();
        }

        if (hasAnyMetadata)
        {
            writer.WritePropertyName("_" + propertyName);
            writer.WriteStartArray();

            foreach (var item in items)
            {
                if (item is not null && HasPrimitiveMetadata(item))
                {
                    WritePrimitiveMetadataObject(writer, (Element)item);
                }
                else
                {
                    writer.WriteNullValue();
                }
            }

            writer.WriteEndArray();
        }

        return true;
    }

    private static void WritePrimitiveMetadataObject(Utf8JsonWriter writer, Element element)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrEmpty(element.Id))
        {
            writer.WriteString("id", element.Id);
        }

        TryWriteProperty(writer, "extension", element.Extension);

        writer.WriteEndObject();
    }

    private static bool WritePrimitiveRawValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing)
    {
        if (TryGetDecimalLiteral(primitive, out var decimalLiteral))
        {
            writer.WriteRawValue(decimalLiteral);
            return true;
        }

        var rawValue = GetPrimitiveRawValue(primitive);
        if (!HasRawJsonValue(rawValue))
        {
            if (writeNullWhenMissing)
            {
                writer.WriteNullValue();
            }

            return false;
        }

        WriteSimpleValue(writer, rawValue!);
        return true;
    }

    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        if (value is string stringValue)
        {
            writer.WriteStringValue(stringValue);
            return;
        }

        if (TryWriteSimpleValue(writer, value))
        {
            return;
        }

        if (IsFhirPrimitive(value.GetType()))
        {
            WritePrimitiveRawValue(writer, value, writeNullWhenMissing: true);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            writer.WriteStartArray();

            foreach (var item in GetSerializableItems(enumerable))
            {
                WriteValue(writer, item!);
            }

            writer.WriteEndArray();
            return;
        }

        WriteObjectValue(writer, value);
    }

    private static bool TryWriteSimpleProperty(Utf8JsonWriter writer, string propertyName, object value)
    {
        switch (value)
        {
            case bool boolValue:
                writer.WriteBoolean(propertyName, boolValue);
                return true;
            case int intValue:
                writer.WriteNumber(propertyName, intValue);
                return true;
            case long longValue:
                writer.WriteNumber(propertyName, longValue);
                return true;
            case decimal decimalValue:
                writer.WriteNumber(propertyName, decimalValue);
                return true;
            case DateTimeOffset dateTimeOffsetValue:
                writer.WriteString(propertyName, FormatDateTimeOffset(dateTimeOffsetValue));
                return true;
            case DateTime dateTimeValue:
                writer.WriteString(propertyName, FormatDateTime(dateTimeValue));
                return true;
            default:
                return false;
        }
    }

    private static bool TryWriteSimpleValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                return true;
            case int intValue:
                writer.WriteNumberValue(intValue);
                return true;
            case long longValue:
                writer.WriteNumberValue(longValue);
                return true;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                return true;
            case DateTimeOffset dateTimeOffsetValue:
                writer.WriteStringValue(FormatDateTimeOffset(dateTimeOffsetValue));
                return true;
            case DateTime dateTimeValue:
                writer.WriteStringValue(FormatDateTime(dateTimeValue));
                return true;
            default:
                return false;
        }
    }

    private static void WriteSimpleValue(Utf8JsonWriter writer, object value)
    {
        if (value is string stringValue)
        {
            writer.WriteStringValue(stringValue);
            return;
        }

        if (TryWriteSimpleValue(writer, value))
        {
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType());
    }

    private static List<object?> GetSerializableItems(IEnumerable enumerable)
    {
        var items = new List<object?>();

        foreach (var item in enumerable)
        {
            if (HasSerializableValue(item))
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static bool HasSerializableValue(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string stringValue)
        {
            return stringValue.Length > 0;
        }

        if (IsFhirPrimitive(value.GetType()))
        {
            return HasPrimitiveRawValue(value) || HasPrimitiveMetadata(value);
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (HasSerializableValue(item))
                {
                    return true;
                }
            }

            return false;
        }

        if (IsSimpleValue(value))
        {
            return true;
        }

        if (value is Resource)
        {
            return true;
        }

        return HasSerializableObjectProperties(value);
    }

    private static bool HasSerializableObjectProperties(object value)
    {
        foreach (var property in GetSerializableProperties(value.GetType()))
        {
            if (property.DeclaringType == typeof(Extension) &&
                property.Name == nameof(Extension.Value))
            {
                if (HasSerializableValue(property.GetValue(value)))
                {
                    return true;
                }

                continue;
            }

            if (HasSerializableValue(property.GetValue(value)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSimpleValue(object value)
    {
        return value is bool
            or int
            or long
            or decimal
            or DateTimeOffset
            or DateTime;
    }

    private static bool HasPrimitiveRawValue(object primitive)
    {
        if (TryGetDecimalLiteral(primitive, out _))
        {
            return true;
        }

        return HasRawJsonValue(GetPrimitiveRawValue(primitive));
    }

    private static bool HasPrimitiveMetadata(object primitive)
    {
        return primitive is Element element &&
            (!string.IsNullOrEmpty(element.Id) || HasSerializableValue(element.Extension));
    }

    private static bool HasRawJsonValue(object? value)
    {
        return value switch
        {
            null => false,
            string stringValue => stringValue.Length > 0,
            _ => true
        };
    }

    private static object? GetPrimitiveRawValue(object primitive)
    {
        return primitive.GetType().GetProperty(nameof(PrimitiveType<object>.Value))?.GetValue(primitive);
    }

    private static bool TryGetDecimalLiteral(object primitive, out string decimalLiteral)
    {
        decimalLiteral = string.Empty;

        if (primitive.GetType().Name != "FhirDecimal")
        {
            return false;
        }

        var literal = primitive.GetType()
            .GetProperty("Literal", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(primitive) as string;

        if (string.IsNullOrEmpty(literal))
        {
            return false;
        }

        decimalLiteral = literal;
        return true;
    }

    private static bool IsFhirPrimitive(Type type)
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

    private static string FormatDateTimeOffset(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }
}
