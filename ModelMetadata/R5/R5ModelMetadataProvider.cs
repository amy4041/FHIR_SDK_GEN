using MyFhirSdk.Validation.Rules;

namespace MyFhirSdk.ModelMetadata.R5;

/// <summary>
/// Owns the generated R5 model-to-Runtime metadata boundary.
/// Runtime engines consume only provider contracts; generated R5 identities
/// and validation entries are composed here.
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

    public ResourceTypeMetadata GetRequiredResource(Type resourceType) =>
        _models.GetRequiredResource(resourceType);

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
            GeneratedR5ModelMetadata.Create(),
            GeneratedR5ValidationRules.Create());
    }

}
