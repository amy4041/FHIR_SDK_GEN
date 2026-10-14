using System.Collections;
using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Rules;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Tests.Architecture.V1;

/// <summary>
/// V1-only composition boundary. Exercises the real SDK engines through existing
/// friend access; neither this guard nor these APIs are production SPI.
/// </summary>
internal sealed class ExperimentContext
{
    private readonly IModelMetadataProvider _metadata;
    private readonly PrimitiveRegistry _primitives;
    private readonly Assembly _models;
    private readonly HashSet<Type>? _selectedModels;
    private readonly FhirJsonParser _parser;
    private readonly FhirJsonSerializer _serializer;
    private readonly FhirValidator _validator;

    internal string Version { get; }
    internal string PackageId { get; }

    private ExperimentContext(string version, string packageId, Assembly models,
        IModelMetadataProvider metadata, PrimitiveRegistry primitives, IValidationRuleProvider rules,
        IEnumerable<Type>? selectedModels = null)
    {
        Version = version;
        PackageId = packageId;
        _models = models;
        _metadata = metadata;
        _primitives = primitives;
        _selectedModels = selectedModels?.ToHashSet();
        if (primitives.Definitions.Any(p => p.PrimitiveType.Assembly != models) ||
            metadata.ConcreteDataTypes.Any(t => t.Assembly != models && t.Assembly != typeof(Meta).Assembly))
            throw new FhirSdkException($"V1 context {version}: mixed model/primitive providers.");
        _parser = new FhirJsonParser(metadata, primitives);
        _serializer = new FhirJsonSerializer(metadata, primitives);
        _validator = new FhirValidator(rules, primitives, new FhirObjectGraphWalker());
    }

    internal static ExperimentContext R4() => new("4.0.1", R4ExperimentComposition.PackageId,
        typeof(MyFhirSdk.R4.Resources.Patient).Assembly, R4ExperimentComposition.CreateMetadata(),
        R4ExperimentComposition.CreatePrimitives(), R4ExperimentComposition.CreateRules(),
        R4ExperimentComposition.ModelTypes);

    internal static ExperimentContext R5() => new("5.0.0", "hl7.fhir.r5.core",
        typeof(MyFhirSdk.Resources.Patient).Assembly, R5ModelMetadataProvider.Default,
        PrimitiveRegistry.Default, R5ModelMetadataProvider.Default);

    internal static ExperimentContext MixedForNegativeTest() => new("4.0.1", "hl7.fhir.r4.core",
        typeof(MyFhirSdk.R4.Resources.Patient).Assembly, R4ExperimentComposition.CreateMetadata(),
        PrimitiveRegistry.Default, R4ExperimentComposition.CreateRules());

    internal Resource CreateResource(string name) => _metadata.GetRequiredResource(name).CreateResource();

    internal T Parse<T>(string json) where T : Resource
    {
        if (!typeof(T).IsAbstract) _metadata.GetRequiredResource(typeof(T)); // Reject foreign concrete CLR targets.
        var result = _parser.Parse<T>(json);
        Check(result);
        return result;
    }

    internal string Serialize(Resource resource)
    {
        Check(resource);
        return _serializer.Serialize(resource);
    }

    internal ValidationResult Validate(Resource resource)
    {
        Check(resource);
        return _validator.Validate(resource);
    }

    private void Check(FhirObject root)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(root, root is Resource r ? r.ResourceType : root.GetType().Name);
        void Visit(object? value, string path)
        {
            if (value is null || value is string || !visited.Add(value)) return;
            if (value is IEnumerable list)
            {
                var index = 0;
                foreach (var item in list) Visit(item, $"{path}[{index++}]");
                return;
            }
            if (value is not FhirObject fhir) return;
            var type = fhir.GetType();
            var shared = type == typeof(Meta) || type == typeof(Narrative) || type == typeof(Extension);
            if (!shared && (type.Assembly != _models ||
                _selectedModels is not null && !_selectedModels.Contains(type) && fhir is not IPrimitiveValueAccessor))
                throw new FhirSdkException($"V1 context {Version}: foreign CLR type '{type.FullName}' at {path}.");
            if (fhir is IPrimitiveValueAccessor) _primitives.GetRequired(type);
            if (fhir is Resource) _metadata.GetRequiredResource(type);
            if (fhir is Meta meta)
            {
                CheckCoding(meta.Tag, "tag");
                CheckCoding(meta.Security, "security");
            }
            if (fhir is Extension { Value: not null } extension)
                _metadata.GetRequiredExtensionValuePropertyName(extension.Value.GetType());
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                Visit(property.GetValue(value), path + "." + FhirJsonConventions.GetJsonPropertyName(property));
            void CheckCoding(IEnumerable<DataType>? items, string field)
            {
                if (items is null) return; // Existing walker diagnoses a null list.
                if (!_metadata.TryGetDeclaredDataType(typeof(Meta), field, out var coding))
                    throw new FhirSdkException($"V1 context {Version}: missing Meta.{field} metadata.");
                if (items.Any(item => item is not null && item.GetType() != coding))
                    throw new FhirSdkException($"V1 context {Version}: {path}.{field} requires this context's Coding.");
            }
        }
    }
}
