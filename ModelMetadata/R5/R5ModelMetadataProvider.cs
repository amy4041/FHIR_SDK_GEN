using MyFhirSdk.Core;
using MyFhirSdk.Validation.Rules;

namespace MyFhirSdk.ModelMetadata.R5;

/// <summary>
/// Owns the handwritten R5 model-to-Runtime metadata boundary.
/// Runtime engines consume only the provider contracts; R5-specific reflection
/// and concrete model references are concentrated here.
/// </summary>
internal sealed class R5ModelMetadataProvider :
    IModelMetadataProvider,
    IValidationRuleProvider
{
    private readonly ImmutableModelMetadataProvider _models;
    private readonly ResourceRuleRegistry _validationRules;

    private R5ModelMetadataProvider(
        ImmutableModelMetadataProvider models,
        ResourceRuleRegistry validationRules)
    {
        _models = models;
        _validationRules = validationRules;
    }

    internal static R5ModelMetadataProvider Default { get; } = CreateDefault();

    public IReadOnlyList<Type> ConcreteDataTypes => _models.ConcreteDataTypes;

    public ResourceTypeMetadata GetRequiredResource(string fhirTypeName) =>
        _models.GetRequiredResource(fhirTypeName);

    public ResourceTypeMetadata GetRequiredResource(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        try
        {
            return _models.GetRequiredResource(resourceType);
        }
        catch (FhirSdkException) when (
            typeof(Resource).IsAssignableFrom(resourceType) &&
            !resourceType.IsAbstract &&
            !resourceType.IsInterface)
        {
            // Generated model assemblies can pass a known concrete TResource
            // without becoming part of the handwritten R5 provider inventory.
            return CreateResourceMetadata(resourceType);
        }
    }

    public bool TryGetDeclaredDataType(
        Type declaringType,
        string propertyName,
        out Type concreteType) =>
        _models.TryGetDeclaredDataType(declaringType, propertyName, out concreteType);

    public string GetRequiredExtensionValuePropertyName(Type valueType) =>
        _models.GetRequiredExtensionValuePropertyName(valueType);

    public bool TryGetExtensionValueType(string propertyName, out Type valueType) =>
        _models.TryGetExtensionValueType(propertyName, out valueType);

    public bool TryGetOpenTypeJsonPropertyName(
        Type declaringType,
        string propertyName,
        Type valueType,
        out string jsonPropertyName) =>
        _models.TryGetOpenTypeJsonPropertyName(
            declaringType,
            propertyName,
            valueType,
            out jsonPropertyName);

    public bool TryGetOpenTypeValueType(
        Type declaringType,
        string propertyName,
        string jsonPropertyName,
        out Type valueType) =>
        _models.TryGetOpenTypeValueType(
            declaringType,
            propertyName,
            jsonPropertyName,
            out valueType);

    public IReadOnlyList<IFhirValidationRule> GetRules(Type type) =>
        _validationRules.GetRules(type);

    private static R5ModelMetadataProvider CreateDefault()
    {
        return new R5ModelMetadataProvider(
            R5HandwrittenModelMetadataEntries.Create(),
            ResourceRuleRegistry.Create(R5ValidationRuleEntries.Create()));
    }

    private static ResourceTypeMetadata CreateResourceMetadata(Type type)
    {
        var resource = (Resource?)Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                $"Could not create R5 Resource metadata for '{type.FullName}'.");

        return new ResourceTypeMetadata(
            resource.ResourceType,
            type,
            () => (Resource)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException(
                    $"Could not create R5 Resource '{type.FullName}'.")));
    }

}
