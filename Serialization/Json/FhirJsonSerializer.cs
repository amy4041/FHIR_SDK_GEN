using System.Collections;
using System.Text;
using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

/// <summary>
/// Serializes typed SDK resources to FHIR JSON.
/// </summary>
public sealed partial class FhirJsonSerializer : IFhirSerializer
{
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
        writer.WriteString("resourceType", FhirJsonConventions.GetResourceTypeName(resource));
        WriteObjectProperties(writer, resource);
        writer.WriteEndObject();
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
        foreach (var property in FhirJsonConventions.GetSerializableProperties(value.GetType()))
        {
            var propertyValue = property.GetValue(value);

            if (property.DeclaringType == typeof(Extension) &&
                property.Name == nameof(Extension.Value))
            {
                TryWriteExtensionValueProperty(writer, propertyValue);
                continue;
            }

            TryWriteProperty(writer, FhirJsonConventions.GetJsonPropertyName(property), propertyValue);
        }
    }

    private static bool TryWriteExtensionValueProperty(Utf8JsonWriter writer, object? value)
    {
        if (!HasSerializableValue(value))
        {
            return false;
        }

        return TryWriteProperty(writer, FhirJsonConventions.GetExtensionValuePropertyName(value!), value);
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

        if (FhirJsonConventions.IsFhirPrimitive(nonNullValue.GetType()))
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

        if (items.TrueForAll(item => item is not null && FhirJsonConventions.IsFhirPrimitive(item.GetType())))
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
}
