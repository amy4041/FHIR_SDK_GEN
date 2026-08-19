using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;
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

    public IReadOnlyList<IFhirValidationRule> GetRules(Type type) =>
        _validationRules.GetRules(type);

    private static R5ModelMetadataProvider CreateDefault()
    {
        var modelAssembly = typeof(Patient).Assembly;
        var modelTypes = modelAssembly
            .GetTypes()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        var resources = modelTypes
            .Where(type =>
                typeof(Resource).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsInterface)
            .Select(CreateResourceMetadata)
            .ToArray();

        var concreteDataTypes = modelTypes
            .Where(type =>
                typeof(DataType).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsInterface &&
                !PrimitiveValueAccess.IsPrimitiveType(type))
            .ToArray();

        var declaredDataTypes = new[]
        {
            new DeclaredDataTypeMetadata(typeof(Meta), "security", typeof(Coding)),
            new DeclaredDataTypeMetadata(typeof(Meta), "tag", typeof(Coding))
        };

        var extensionValues = modelTypes
            .Where(type =>
                typeof(IFhirExtensionValue).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsInterface)
            .Select(type => new ExtensionValueMetadata(
                type,
                GetExtensionValuePropertyName(type),
                type != typeof(SimpleQuantity)))
            .ToArray();

        return new R5ModelMetadataProvider(
            new ImmutableModelMetadataProvider(
                resources,
                concreteDataTypes,
                declaredDataTypes,
                extensionValues),
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

    private static string GetExtensionValuePropertyName(Type type)
    {
        if (type == typeof(SimpleQuantity))
        {
            return "valueQuantity";
        }

        var name = type.Name.StartsWith("Fhir", StringComparison.Ordinal)
            ? type.Name["Fhir".Length..]
            : type.Name;

        return $"value{name}";
    }
}
