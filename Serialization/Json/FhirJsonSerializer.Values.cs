using System.Collections;
using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonSerializer
{
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

        if (FhirJsonConventions.IsFhirPrimitive(value.GetType()))
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
                writer.WriteString(propertyName, FhirJsonConventions.FormatDateTimeOffset(dateTimeOffsetValue));
                return true;
            case DateTime dateTimeValue:
                writer.WriteString(propertyName, FhirJsonConventions.FormatDateTime(dateTimeValue));
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
                writer.WriteStringValue(FhirJsonConventions.FormatDateTimeOffset(dateTimeOffsetValue));
                return true;
            case DateTime dateTimeValue:
                writer.WriteStringValue(FhirJsonConventions.FormatDateTime(dateTimeValue));
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

        if (FhirJsonConventions.IsFhirPrimitive(value.GetType()))
        {
            return FhirJsonConventions.HasPrimitiveRawValue(value) || HasPrimitiveMetadata(value);
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

        if (FhirJsonConventions.IsSimpleValue(value))
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
        foreach (var property in FhirJsonConventions.GetSerializableProperties(value.GetType()))
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
}
