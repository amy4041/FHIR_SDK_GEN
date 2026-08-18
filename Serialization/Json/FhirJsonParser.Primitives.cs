using System.Collections;
using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonParser
{
    private static readonly PrimitiveRegistry PrimitiveDefinitions =
        PrimitiveRegistry.Default;

    private static object? ReadPrimitiveValue(
        Type primitiveType,
        JsonElement? rawElement,
        JsonElement? metadataElement)
    {
        if (!HasJsonValue(rawElement) && !HasJsonValue(metadataElement))
        {
            return null;
        }

        var definition = PrimitiveDefinitions.GetRequired(primitiveType);
        var primitive = definition.Codec.CreatePrimitive(
            primitiveType,
            HasJsonValue(rawElement) ? rawElement : null);

        if (HasJsonValue(metadataElement))
        {
            ReadPrimitiveMetadata(primitive, metadataElement!.Value);
        }

        return primitive;
    }

    private static void ReadPrimitiveListValue(
        IList list,
        Type primitiveType,
        string propertyName,
        JsonElement? rawElement,
        JsonElement? metadataElement)
    {
        var rawItems = ReadOptionalArray(rawElement, propertyName);
        var metadataItems = ReadOptionalArray(metadataElement, FhirJsonConventions.GetPrimitiveMetadataPropertyName(propertyName));
        var itemCount = Math.Max(rawItems.Count, metadataItems.Count);

        for (var index = 0; index < itemCount; index++)
        {
            var rawItem = index < rawItems.Count ? rawItems[index] : (JsonElement?)null;
            var metadataItem = index < metadataItems.Count ? metadataItems[index] : (JsonElement?)null;
            var primitive = ReadPrimitiveValue(primitiveType, rawItem, metadataItem);

            if (primitive is not null)
            {
                list.Add(primitive);
            }
        }
    }

    private static void ReadPrimitiveMetadata(object primitive, JsonElement metadataElement)
    {
        if (metadataElement.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        EnsureObject(metadataElement, "primitive metadata");
        ReadObjectProperties(metadataElement, primitive);
    }

    private static List<JsonElement> ReadOptionalArray(JsonElement? element, string propertyName)
    {
        var items = new List<JsonElement>();

        if (!HasJsonValue(element))
        {
            return items;
        }

        EnsureArray(element!.Value, propertyName);

        foreach (var item in element.Value.EnumerateArray())
        {
            items.Add(item);
        }

        return items;
    }

}
