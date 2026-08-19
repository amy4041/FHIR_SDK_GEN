using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;

namespace MyFhirSdk.Serialization.Json;

/// <summary>
/// Parses FHIR JSON into typed SDK resources.
/// </summary>
public sealed partial class FhirJsonParser : IFhirParser
{
    private readonly IModelMetadataProvider _metadataProvider;

    public FhirJsonParser()
        : this(R5ModelMetadataProvider.Default)
    {
    }

    internal FhirJsonParser(IModelMetadataProvider metadataProvider)
    {
        _metadataProvider = metadataProvider
            ?? throw new ArgumentNullException(nameof(metadataProvider));
    }

    public TResource Parse<TResource>(string json)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        var resource = ReadResource(document.RootElement, typeof(TResource));

        return (TResource)resource;
    }

    private Resource ReadResource(JsonElement element, Type expectedType)
    {
        EnsureObject(element, "FHIR resource");

        var resourceTypeName = ReadResourceTypeName(element);
        var resourceMetadata = ResolveResourceType(resourceTypeName, expectedType);
        var resource = resourceMetadata.CreateResource();

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

    private ResourceTypeMetadata ResolveResourceType(string resourceTypeName, Type expectedType)
    {
        if (IsConcreteType(expectedType))
        {
            var expectedResource = _metadataProvider.GetRequiredResource(expectedType);

            if (!string.Equals(resourceTypeName, expectedResource.FhirTypeName, StringComparison.Ordinal))
            {
                throw new FhirSdkException(
                    $"FHIR JSON resourceType '{resourceTypeName}' does not match expected resource type '{expectedResource.FhirTypeName}'.");
            }

            return expectedResource;
        }

        var resource = _metadataProvider.GetRequiredResource(resourceTypeName);

        if (!expectedType.IsAssignableFrom(resource.ResourceType))
        {
            throw new FhirSdkException(
                $"FHIR JSON resourceType '{resourceTypeName}' cannot be assigned to '{expectedType.Name}'.");
        }

        return resource;
    }

    private bool TryResolveExtensionValueType(string propertyName, out Type valueType)
    {
        return _metadataProvider.TryGetExtensionValueType(propertyName, out valueType);
    }

    private Type ResolveObjectType(
        Type declaredType,
        Type declaringType,
        JsonElement element,
        string propertyName)
    {
        if (IsConcreteType(declaredType))
        {
            return declaredType;
        }

        if (typeof(Resource).IsAssignableFrom(declaredType))
        {
            var resourceTypeName = ReadResourceTypeName(element);
            return ResolveResourceType(resourceTypeName, declaredType).ResourceType;
        }

        if (declaredType == typeof(DataType))
        {
            return ResolveDataType(declaringType, element, propertyName);
        }

        throw new FhirSdkException($"Cannot infer a concrete type for JSON property '{propertyName}' declared as '{declaredType.Name}'.");
    }

    private Type ResolveDataType(
        Type declaringType,
        JsonElement element,
        string propertyName)
    {
        if (_metadataProvider.TryGetDeclaredDataType(
            declaringType,
            propertyName,
            out var declaredDataType))
        {
            return declaredDataType;
        }

        var jsonPropertyNames = GetObjectPropertyNames(element);
        var matchingTypes = _metadataProvider.ConcreteDataTypes
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

}
