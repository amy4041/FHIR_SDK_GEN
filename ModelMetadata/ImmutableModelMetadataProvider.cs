using System.Collections.ObjectModel;
using MyFhirSdk.Core;

namespace MyFhirSdk.ModelMetadata;

internal sealed class ImmutableModelMetadataProvider : IModelMetadataProvider
{
    private readonly IReadOnlyDictionary<string, ResourceTypeMetadata> _resourcesByName;
    private readonly IReadOnlyDictionary<Type, ResourceTypeMetadata> _resourcesByType;
    private readonly IReadOnlyDictionary<DeclaredPropertyKey, Type> _declaredDataTypes;
    private readonly IReadOnlyDictionary<Type, string> _extensionPropertiesByType;
    private readonly IReadOnlyDictionary<string, Type> _extensionTypesByProperty;

    internal ImmutableModelMetadataProvider(
        IEnumerable<ResourceTypeMetadata> resources,
        IEnumerable<Type> concreteDataTypes,
        IEnumerable<DeclaredDataTypeMetadata> declaredDataTypes,
        IEnumerable<ExtensionValueMetadata> extensionValues)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(concreteDataTypes);
        ArgumentNullException.ThrowIfNull(declaredDataTypes);
        ArgumentNullException.ThrowIfNull(extensionValues);

        var resourceEntries = resources.ToArray();
        EnsureUnique(
            resourceEntries,
            entry => entry.FhirTypeName,
            StringComparer.Ordinal,
            name => $"Duplicate FHIR resource type name '{name}'.");
        EnsureUnique(
            resourceEntries,
            entry => entry.ResourceType,
            EqualityComparer<Type>.Default,
            type => $"Duplicate Resource CLR type '{type.FullName}'.");

        _resourcesByName = new ReadOnlyDictionary<string, ResourceTypeMetadata>(
            resourceEntries.ToDictionary(
                entry => entry.FhirTypeName,
                StringComparer.Ordinal));
        _resourcesByType = new ReadOnlyDictionary<Type, ResourceTypeMetadata>(
            resourceEntries.ToDictionary(entry => entry.ResourceType));

        var dataTypeEntries = concreteDataTypes.ToArray();
        EnsureUnique(
            dataTypeEntries,
            type => type,
            EqualityComparer<Type>.Default,
            type => $"Duplicate concrete DataType CLR type '{type.FullName}'.");
        ConcreteDataTypes = dataTypeEntries
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        ValidateConcreteDataTypes(ConcreteDataTypes);

        var declaredEntries = declaredDataTypes.ToArray();
        ValidateDeclaredDataTypes(declaredEntries);
        EnsureUnique(
            declaredEntries,
            entry => new DeclaredPropertyKey(
                entry.DeclaringType,
                entry.PropertyName),
            EqualityComparer<DeclaredPropertyKey>.Default,
            key => $"Duplicate declared datatype metadata for " +
                $"'{key.DeclaringType.FullName}.{key.PropertyName}'.");
        _declaredDataTypes = new ReadOnlyDictionary<DeclaredPropertyKey, Type>(
            declaredEntries.ToDictionary(
                entry => new DeclaredPropertyKey(
                    entry.DeclaringType,
                    entry.PropertyName),
                entry => entry.ConcreteType));

        var extensionEntries = extensionValues.ToArray();
        ValidateExtensionEntries(extensionEntries);
        _extensionPropertiesByType = new ReadOnlyDictionary<Type, string>(
            extensionEntries.ToDictionary(
                entry => entry.ValueType,
                entry => entry.PropertyName));
        _extensionTypesByProperty = new ReadOnlyDictionary<string, Type>(
            extensionEntries
                .Where(entry => entry.IsParserTarget)
                .ToDictionary(
                    entry => entry.PropertyName,
                    entry => entry.ValueType,
                    StringComparer.Ordinal));
    }

    public IReadOnlyList<Type> ConcreteDataTypes { get; }

    public ResourceTypeMetadata GetRequiredResource(string fhirTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirTypeName);

        return _resourcesByName.TryGetValue(fhirTypeName, out var metadata)
            ? metadata
            : throw new FhirSdkException(
                $"FHIR JSON resourceType '{fhirTypeName}' is not supported by this SDK.");
    }

    public ResourceTypeMetadata GetRequiredResource(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        return _resourcesByType.TryGetValue(resourceType, out var metadata)
            ? metadata
            : throw new FhirSdkException(
                $"FHIR Resource CLR type '{resourceType.FullName}' is not registered.");
    }

    public bool TryGetDeclaredDataType(
        Type declaringType,
        string propertyName,
        out Type concreteType)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return _declaredDataTypes.TryGetValue(
            new DeclaredPropertyKey(declaringType, propertyName),
            out concreteType!);
    }

    public string GetRequiredExtensionValuePropertyName(Type valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);

        for (var current = valueType; current is not null; current = current.BaseType)
        {
            if (_extensionPropertiesByType.TryGetValue(current, out var propertyName))
            {
                return propertyName;
            }
        }

        throw new FhirSdkException(
            $"FHIR Extension value type '{valueType.FullName}' is not registered.");
    }

    public bool TryGetExtensionValueType(
        string propertyName,
        out Type valueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return _extensionTypesByProperty.TryGetValue(propertyName, out valueType!);
    }

    private static void ValidateConcreteDataTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (!typeof(DataType).IsAssignableFrom(type) ||
                type.IsAbstract ||
                type.IsInterface)
            {
                throw new ArgumentException(
                    $"'{type.FullName}' is not a concrete FHIR DataType.",
                    nameof(types));
            }
        }
    }

    private static void ValidateDeclaredDataTypes(
        IEnumerable<DeclaredDataTypeMetadata> entries)
    {
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.DeclaringType);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.PropertyName);
            ArgumentNullException.ThrowIfNull(entry.ConcreteType);

            if (!typeof(DataType).IsAssignableFrom(entry.ConcreteType) ||
                entry.ConcreteType.IsAbstract ||
                entry.ConcreteType.IsInterface)
            {
                throw new ArgumentException(
                    $"'{entry.ConcreteType.FullName}' is not a concrete FHIR DataType.",
                    nameof(entries));
            }
        }
    }

    private static void ValidateExtensionEntries(
        IReadOnlyList<ExtensionValueMetadata> entries)
    {
        EnsureUnique(
            entries,
            entry => entry.ValueType,
            EqualityComparer<Type>.Default,
            type => $"Duplicate Extension value CLR type '{type.FullName}'.");
        EnsureUnique(
            entries.Where(entry => entry.IsParserTarget),
            entry => entry.PropertyName,
            StringComparer.Ordinal,
            name => $"Duplicate parser Extension value property '{name}'.");

        foreach (var entry in entries)
        {
            if (!typeof(IFhirExtensionValue).IsAssignableFrom(entry.ValueType) ||
                entry.ValueType.IsAbstract ||
                entry.ValueType.IsInterface)
            {
                throw new ArgumentException(
                    $"'{entry.ValueType.FullName}' is not a concrete FHIR Extension value type.",
                    nameof(entries));
            }

            if (string.IsNullOrWhiteSpace(entry.PropertyName) ||
                !entry.PropertyName.StartsWith("value", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{entry.PropertyName}' is not a valid Extension value[x] property name.",
                    nameof(entries));
            }
        }
    }

    private static void EnsureUnique<TEntry, TKey>(
        IEnumerable<TEntry> entries,
        Func<TEntry, TKey> getKey,
        IEqualityComparer<TKey> comparer,
        Func<TKey, string> getMessage)
        where TKey : notnull
    {
        var duplicate = entries
            .GroupBy(getKey, comparer)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(getMessage(duplicate.Key));
        }
    }

    private readonly record struct DeclaredPropertyKey(
        Type DeclaringType,
        string PropertyName);
}
