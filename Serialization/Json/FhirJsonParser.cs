using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization.Json;

/// <summary>
/// Parses FHIR JSON into typed SDK resources.
/// </summary>
public sealed partial class FhirJsonParser : IFhirParser
{
    private static readonly IReadOnlyDictionary<string, Type> ResourceTypesByName = BuildResourceTypesByName();
    private static readonly IReadOnlyDictionary<string, Type> ExtensionValueTypesByPropertyName = BuildExtensionValueTypesByPropertyName();
    private static readonly Type[] ComplexDataTypes = BuildComplexDataTypes();

    public TResource Parse<TResource>(string json)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        var resource = ReadResource(document.RootElement, typeof(TResource));

        return (TResource)resource;
    }

    private static Resource ReadResource(JsonElement element, Type expectedType)
    {
        EnsureObject(element, "FHIR resource");

        var resourceTypeName = ReadResourceTypeName(element);
        var resourceType = ResolveResourceType(resourceTypeName, expectedType);
        var resource = (Resource)CreateInstance(resourceType);

        ReadObjectProperties(element, resource);

        return resource;
    }

    private static string ReadResourceTypeName(JsonElement element)
    {
        if (!element.TryGetProperty("resourceType", out var resourceTypeElement) ||
            resourceTypeElement.ValueKind != JsonValueKind.String)
        {
            throw new FhirSdkException("FHIR JSON resource is missing a string resourceType property.");
        }

        var resourceTypeName = resourceTypeElement.GetString();
        if (string.IsNullOrWhiteSpace(resourceTypeName))
        {
            throw new FhirSdkException("FHIR JSON resourceType cannot be empty.");
        }

        return resourceTypeName;
    }

    private static Type ResolveResourceType(string resourceTypeName, Type expectedType)
    {
        if (IsConcreteType(expectedType))
        {
            var expectedResource = (Resource)CreateInstance(expectedType);
            var expectedResourceTypeName = FhirJsonConventions.GetResourceTypeName(expectedResource);

            if (!string.Equals(resourceTypeName, expectedResourceTypeName, StringComparison.Ordinal))
            {
                throw new FhirSdkException(
                    $"FHIR JSON resourceType '{resourceTypeName}' does not match expected resource type '{expectedResourceTypeName}'.");
            }

            return expectedType;
        }

        if (!ResourceTypesByName.TryGetValue(resourceTypeName, out var resourceType))
        {
            throw new FhirSdkException($"FHIR JSON resourceType '{resourceTypeName}' is not supported by this SDK.");
        }

        if (!expectedType.IsAssignableFrom(resourceType))
        {
            throw new FhirSdkException(
                $"FHIR JSON resourceType '{resourceTypeName}' cannot be assigned to '{expectedType.Name}'.");
        }

        return resourceType;
    }

    private static bool TryResolveExtensionValueType(string propertyName, out Type valueType)
    {
        return ExtensionValueTypesByPropertyName.TryGetValue(propertyName, out valueType!);
    }

    private static Type ResolveObjectType(Type declaredType, JsonElement element, string propertyName)
    {
        if (IsConcreteType(declaredType))
        {
            return declaredType;
        }

        if (typeof(Resource).IsAssignableFrom(declaredType))
        {
            var resourceTypeName = ReadResourceTypeName(element);
            return ResolveResourceType(resourceTypeName, declaredType);
        }

        if (declaredType == typeof(DataType))
        {
            return ResolveDataType(element, propertyName);
        }

        throw new FhirSdkException($"Cannot infer a concrete type for JSON property '{propertyName}' declared as '{declaredType.Name}'.");
    }

    private static Type ResolveDataType(JsonElement element, string propertyName)
    {
        if (string.Equals(propertyName, "security", StringComparison.Ordinal) ||
            string.Equals(propertyName, "tag", StringComparison.Ordinal))
        {
            var codingType = typeof(Resource).Assembly.GetType("MyFhirSdk.Types.Coding");
            if (codingType is not null)
            {
                return codingType;
            }
        }

        var jsonPropertyNames = GetObjectPropertyNames(element);
        var matchingTypes = ComplexDataTypes
            .Select(type => new
            {
                Type = type,
                MatchCount = CountMatchingProperties(type, jsonPropertyNames)
            })
            .Where(match => match.MatchCount == jsonPropertyNames.Count)
            .OrderBy(match => match.Type.Name, StringComparer.Ordinal)
            .ToArray();

        if (matchingTypes.Length == 1)
        {
            return matchingTypes[0].Type;
        }

        throw new FhirSdkException($"Cannot infer a concrete datatype for JSON property '{propertyName}'.");
    }

    private static HashSet<string> GetObjectPropertyNames(JsonElement element)
    {
        EnsureObject(element, "FHIR datatype");

        var propertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "id" ||
                property.Name == "extension" ||
                property.Name == "modifierExtension" ||
                property.Name.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            propertyNames.Add(property.Name);
        }

        return propertyNames;
    }

    private static int CountMatchingProperties(Type type, HashSet<string> jsonPropertyNames)
    {
        var supportedPropertyNames = FhirJsonConventions.GetSerializableProperties(type)
            .Select(FhirJsonConventions.GetJsonPropertyName)
            .ToHashSet(StringComparer.Ordinal);

        var count = 0;

        foreach (var propertyName in jsonPropertyNames)
        {
            if (!supportedPropertyNames.Contains(propertyName))
            {
                return -1;
            }

            count++;
        }

        return count;
    }

    private static object CreateInstance(Type type)
    {
        return Activator.CreateInstance(type)
            ?? throw new FhirSdkException($"Could not create an instance of '{type.FullName}'.");
    }

    private static void EnsureObject(JsonElement element, string valueName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FhirSdkException($"{valueName} must be a JSON object.");
        }
    }

    private static void EnsureArray(JsonElement element, string valueName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FhirSdkException($"{valueName} must be a JSON array.");
        }
    }

    private static bool IsConcreteType(Type type)
    {
        return !type.IsAbstract &&
            !type.IsInterface &&
            type.GetConstructor(Type.EmptyTypes) is not null;
    }

    private static IReadOnlyDictionary<string, Type> BuildResourceTypesByName()
    {
        var resourceTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in typeof(Resource).Assembly.GetTypes())
        {
            if (!typeof(Resource).IsAssignableFrom(type) || !IsConcreteType(type))
            {
                continue;
            }

            var resource = (Resource)CreateInstance(type);
            var resourceTypeName = FhirJsonConventions.GetResourceTypeName(resource);

            resourceTypes[resourceTypeName] = type;
        }

        return resourceTypes;
    }

    private static IReadOnlyDictionary<string, Type> BuildExtensionValueTypesByPropertyName()
    {
        var valueTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in typeof(Resource).Assembly.GetTypes())
        {
            if (!typeof(IFhirExtensionValue).IsAssignableFrom(type) || !IsConcreteType(type))
            {
                continue;
            }

            valueTypes[FhirJsonConventions.GetExtensionValuePropertyName(type)] = type;
        }

        return valueTypes;
    }

    private static Type[] BuildComplexDataTypes()
    {
        return typeof(Resource).Assembly.GetTypes()
            .Where(type => typeof(DataType).IsAssignableFrom(type))
            .Where(IsConcreteType)
            .Where(type => !FhirJsonConventions.IsFhirPrimitive(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
