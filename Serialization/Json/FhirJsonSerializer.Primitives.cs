using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonSerializer
{
    private static readonly PrimitiveRegistry PrimitiveDefinitions =
        PrimitiveRegistry.Default;

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
            writer.WritePropertyName(FhirJsonConventions.GetPrimitiveMetadataPropertyName(propertyName));
            WritePrimitiveMetadataObject(writer, (Element)primitive);
        }

        return true;
    }

    private static bool WritePrimitiveArrayProperty(
        Utf8JsonWriter writer,
        string propertyName,
        List<object?> items)
    {
        var hasAnyRawValue = items.Any(
            item => item is not null && HasPrimitiveRawValue(item));
        var hasAnyMetadata = items.Any(item => item is not null && HasPrimitiveMetadata(item));

        if (!hasAnyRawValue && !hasAnyMetadata)
        {
            return false;
        }

        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var item in items)
        {
            if (item is null)
            {
                writer.WriteNullValue();
                continue;
            }

            WritePrimitiveRawValue(writer, item, writeNullWhenMissing: true);
        }

        writer.WriteEndArray();

        if (hasAnyMetadata)
        {
            writer.WritePropertyName(FhirJsonConventions.GetPrimitiveMetadataPropertyName(propertyName));
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
        var codec = PrimitiveDefinitions
            .GetRequired(primitive.GetType())
            .Codec;
        var hasRawValue = codec.HasRawValue(primitive);

        codec.WriteRawValue(writer, primitive, writeNullWhenMissing);
        return hasRawValue;
    }

    private static bool HasPrimitiveRawValue(object primitive)
    {
        return PrimitiveDefinitions
            .GetRequired(primitive.GetType())
            .Codec
            .HasRawValue(primitive);
    }

    private static bool HasPrimitiveMetadata(object primitive)
    {
        return primitive is Element element &&
            (!string.IsNullOrEmpty(element.Id) || HasSerializableValue(element.Extension));
    }
}
