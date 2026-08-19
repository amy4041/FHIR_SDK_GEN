using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

public sealed partial class FhirJsonParser
{
    private void ReadObjectProperties(JsonElement objectElement, object target)
    {
        EnsureObject(objectElement, target.GetType().Name);

        foreach (var property in FhirJsonConventions.GetSerializableProperties(target.GetType()))
        {
            if (property.DeclaringType == typeof(Extension) &&
                property.Name == nameof(Extension.Value))
            {
                TryReadExtensionValueProperty(objectElement, (Extension)target, property);
                continue;
            }

            var propertyName = FhirJsonConventions.GetJsonPropertyName(property);
            var metadataPropertyName = FhirJsonConventions.GetPrimitiveMetadataPropertyName(propertyName);
            var hasRawValue = objectElement.TryGetProperty(propertyName, out var rawElement);
            var hasMetadata = objectElement.TryGetProperty(metadataPropertyName, out var metadataElement);

            if (!hasRawValue && !hasMetadata)
            {
                continue;
            }

            var propertyValue = ReadPropertyValue(
                property.PropertyType,
                property.DeclaringType ?? target.GetType(),
                propertyName,
                hasRawValue ? rawElement : null,
                hasMetadata ? metadataElement : null);

            if (propertyValue is not null)
            {
                SetPropertyValue(target, property, propertyValue);
            }
        }
    }

    private object? ReadPropertyValue(
        Type propertyType,
        Type declaringType,
        string propertyName,
        JsonElement? rawElement,
        JsonElement? metadataElement)
    {
        if (TryGetListElementType(propertyType, out var elementType))
        {
            return ReadListValue(elementType, declaringType, propertyName, rawElement, metadataElement);
        }

        if (FhirJsonConventions.IsFhirPrimitive(propertyType))
        {
            return ReadPrimitiveValue(propertyType, rawElement, metadataElement);
        }

        if (!HasJsonValue(rawElement))
        {
            return null;
        }

        return ReadSingleValue(propertyType, declaringType, propertyName, rawElement!.Value);
    }

    private object ReadListValue(
        Type elementType,
        Type declaringType,
        string propertyName,
        JsonElement? rawElement,
        JsonElement? metadataElement)
    {
        var list = CreateList(elementType);

        if (FhirJsonConventions.IsFhirPrimitive(elementType))
        {
            ReadPrimitiveListValue(list, elementType, propertyName, rawElement, metadataElement);
            return list;
        }

        if (!HasJsonValue(rawElement))
        {
            return list;
        }

        var rawArray = rawElement!.Value;
        EnsureArray(rawArray, propertyName);

        foreach (var item in rawArray.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var itemValue = ReadSingleValue(elementType, declaringType, propertyName, item);
            if (itemValue is not null)
            {
                list.Add(itemValue);
            }
        }

        return list;
    }

    private object? ReadSingleValue(
        Type targetType,
        Type declaringType,
        string propertyName,
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (targetType == typeof(string) || FhirJsonConventions.IsSimpleValueType(targetType))
        {
            return ReadSimpleValue(targetType, element);
        }

        if (typeof(Resource).IsAssignableFrom(targetType))
        {
            return ReadResource(element, targetType);
        }

        EnsureObject(element, propertyName);

        var concreteType = ResolveObjectType(targetType, declaringType, element, propertyName);
        var value = CreateInstance(concreteType);
        ReadObjectProperties(element, value);

        return value;
    }

    private void TryReadExtensionValueProperty(
        JsonElement objectElement,
        Extension extension,
        PropertyInfo property)
    {
        foreach (var jsonProperty in objectElement.EnumerateObject())
        {
            var valuePropertyName = jsonProperty.Name.StartsWith("_", StringComparison.Ordinal)
                ? jsonProperty.Name[1..]
                : jsonProperty.Name;

            if (!valuePropertyName.StartsWith("value", StringComparison.Ordinal) ||
                !TryResolveExtensionValueType(valuePropertyName, out var valueType))
            {
                continue;
            }

            var hasRawValue = objectElement.TryGetProperty(valuePropertyName, out var rawElement);
            var metadataPropertyName = FhirJsonConventions.GetPrimitiveMetadataPropertyName(valuePropertyName);
            var hasMetadata = objectElement.TryGetProperty(metadataPropertyName, out var metadataElement);
            var propertyValue = ReadPropertyValue(
                valueType,
                property.DeclaringType ?? typeof(Extension),
                FhirJsonConventions.GetJsonPropertyName(property),
                hasRawValue ? rawElement : null,
                hasMetadata ? metadataElement : null);

            if (propertyValue is IFhirExtensionValue extensionValue)
            {
                extension.Value = extensionValue;
            }

            return;
        }
    }

    private static object? ReadSimpleValue(Type targetType, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (nonNullableType == typeof(string))
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new FhirSdkException("Expected a JSON string value.");
            }

            return element.GetString();
        }

        if (nonNullableType == typeof(bool))
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new FhirSdkException("Expected a JSON boolean value.")
            };
        }

        if (nonNullableType == typeof(int))
        {
            return element.GetInt32();
        }

        if (nonNullableType == typeof(long))
        {
            return element.GetInt64();
        }

        if (nonNullableType == typeof(decimal))
        {
            return element.GetDecimal();
        }

        if (nonNullableType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(
                ReadString(element),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        if (nonNullableType == typeof(DateTime))
        {
            return DateTime.Parse(
                ReadString(element),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        throw new FhirSdkException($"Unsupported simple JSON value type '{targetType.Name}'.");
    }

    private static string ReadString(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new FhirSdkException("Expected a JSON string value.");
        }

        return element.GetString() ?? string.Empty;
    }

    private static void SetPropertyValue(object target, PropertyInfo property, object value)
    {
        if (property.SetMethod is null)
        {
            return;
        }

        property.SetValue(target, value);
    }

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(IList<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        var listInterface = type.GetInterfaces()
            .FirstOrDefault(currentType =>
                currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition() == typeof(IList<>));

        if (listInterface is not null)
        {
            elementType = listInterface.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static IList CreateList(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);

        return (IList)CreateInstance(listType);
    }

    private static bool HasJsonValue(JsonElement? element)
    {
        return element.HasValue && element.Value.ValueKind != JsonValueKind.Null;
    }
}
