using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonSerializer
{
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
}
