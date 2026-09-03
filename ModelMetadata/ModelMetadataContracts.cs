using MyFhirSdk.Core;

namespace MyFhirSdk.ModelMetadata;

internal interface IModelMetadataProvider
{
    ResourceTypeMetadata GetRequiredResource(string fhirTypeName);

    ResourceTypeMetadata GetRequiredResource(Type resourceType);

    IReadOnlyList<Type> ConcreteDataTypes { get; }

    bool TryGetDeclaredDataType(
        Type declaringType,
        string propertyName,
        out Type concreteType);

    string GetRequiredExtensionValuePropertyName(Type valueType);

    bool TryGetExtensionValueType(
        string propertyName,
        out Type valueType);

    bool TryGetOpenTypeJsonPropertyName(
        Type declaringType,
        string propertyName,
        Type valueType,
        out string jsonPropertyName);

    bool TryGetOpenTypeValueType(
        Type declaringType,
        string propertyName,
        string jsonPropertyName,
        out Type valueType);
}

internal sealed class ResourceTypeMetadata
{
    private readonly Func<Resource> _factory;

    internal ResourceTypeMetadata(
        string fhirTypeName,
        Type resourceType,
        Func<Resource> factory)
    {
        if (string.IsNullOrWhiteSpace(fhirTypeName))
        {
            throw new ArgumentException(
                "FHIR resource type name is required.",
                nameof(fhirTypeName));
        }

        ArgumentNullException.ThrowIfNull(resourceType);
        ArgumentNullException.ThrowIfNull(factory);

        if (!typeof(Resource).IsAssignableFrom(resourceType) ||
            resourceType.IsAbstract ||
            resourceType.IsInterface)
        {
            throw new ArgumentException(
                $"'{resourceType.FullName}' is not a concrete FHIR Resource type.",
                nameof(resourceType));
        }

        FhirTypeName = fhirTypeName;
        ResourceType = resourceType;
        _factory = factory;
    }

    internal string FhirTypeName { get; }

    internal Type ResourceType { get; }

    internal Resource CreateResource()
    {
        Resource resource;

        try
        {
            resource = _factory();
        }
        catch (Exception exception)
        {
            throw new FhirSdkException(
                $"Could not create FHIR resource type '{FhirTypeName}'.",
                exception);
        }

        if (resource is null || !ResourceType.IsInstanceOfType(resource))
        {
            throw new FhirSdkException(
                $"Factory for FHIR resource type '{FhirTypeName}' did not " +
                $"return '{ResourceType.FullName}'.");
        }

        return resource;
    }
}

internal readonly record struct DeclaredDataTypeMetadata(
    Type DeclaringType,
    string PropertyName,
    Type ConcreteType);

internal readonly record struct ExtensionValueMetadata(
    Type ValueType,
    string PropertyName,
    bool IsParserTarget = true);

internal readonly record struct OpenTypeValueMetadata(
    Type DeclaringType,
    string PropertyName,
    Type ValueType,
    string JsonPropertyName);
