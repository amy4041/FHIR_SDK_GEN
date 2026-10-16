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
    private static IModelMetadataProvider Models => R5ModelContext.Default.Metadata;
    internal static R5ModelMetadataProvider Default { get; } = new();

    public IReadOnlyList<Type> ConcreteDataTypes => Models.ConcreteDataTypes;

    public ResourceTypeMetadata GetRequiredResource(string fhirTypeName) =>
        Models.GetRequiredResource(fhirTypeName);

    public ResourceTypeMetadata GetRequiredResource(Type resourceType) =>
        Models.GetRequiredResource(resourceType);

    public bool TryGetDeclaredDataType(
        Type declaringType,
        string propertyName,
        out Type concreteType) =>
        Models.TryGetDeclaredDataType(declaringType, propertyName, out concreteType);

    public string GetRequiredExtensionValuePropertyName(Type valueType) =>
        Models.GetRequiredExtensionValuePropertyName(valueType);

    public bool TryGetExtensionValueType(string propertyName, out Type valueType) =>
        Models.TryGetExtensionValueType(propertyName, out valueType);

    public bool TryGetOpenTypeJsonPropertyName(
        Type declaringType,
        string propertyName,
        Type valueType,
        out string jsonPropertyName) =>
        Models.TryGetOpenTypeJsonPropertyName(
            declaringType,
            propertyName,
            valueType,
            out jsonPropertyName);

    public bool TryGetOpenTypeValueType(
        Type declaringType,
        string propertyName,
        string jsonPropertyName,
        out Type valueType) =>
        Models.TryGetOpenTypeValueType(
            declaringType,
            propertyName,
            jsonPropertyName,
            out valueType);

    public IReadOnlyList<IFhirValidationRule> GetRules(Type type) =>
        R5ModelContext.Default.Rules.GetRules(type);

}
