using System.Collections;
using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonParser
{
    private static object? ReadPrimitiveValue(
        Type primitiveType,
        JsonElement? rawElement,
        JsonElement? metadataElement)
    {
        if (!HasJsonValue(rawElement) && !HasJsonValue(metadataElement))
        {
            return null;
        }

        var primitive = CreatePrimitiveInstance(primitiveType, rawElement);

        if (HasJsonValue(rawElement) &&
            primitiveType.Name != "FhirDecimal")
        {
            SetPrimitiveRawValue(primitive, rawElement!.Value);
        }

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

    private static object CreatePrimitiveInstance(Type primitiveType, JsonElement? rawElement)
    {
        if (primitiveType.Name == "FhirDecimal" && HasJsonValue(rawElement))
        {
            if (rawElement!.Value.ValueKind != JsonValueKind.Number)
            {
                throw new FhirSdkException("FHIR decimal values must be JSON numbers.");
            }

            return Activator.CreateInstance(primitiveType, rawElement.Value.GetRawText())
                ?? throw new FhirSdkException($"Could not create an instance of '{primitiveType.FullName}'.");
        }

        return CreateInstance(primitiveType);
    }

    private static void SetPrimitiveRawValue(object primitive, JsonElement rawElement)
    {
        if (rawElement.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var valueProperty = GetRequiredPrimitiveValueProperty(primitive.GetType());
        var value = ReadSimpleValue(valueProperty.PropertyType, rawElement);

        valueProperty.SetValue(primitive, value);
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

    private static PropertyInfo GetRequiredPrimitiveValueProperty(Type primitiveType)
    {
        return FhirJsonConventions.GetPrimitiveValueProperty(primitiveType)
            ?? throw new FhirSdkException($"FHIR primitive type '{primitiveType.Name}' does not expose a Value property.");
    }
}
