using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.ModelMetadata.R5;

/// <summary>Compatibility composition root for the existing default R5 SDK.</summary>
public static class R5ModelContext
{
    public static FhirModelContext Default { get; } = Create();

    private static FhirModelContext Create()
    {
        var identity = new ModelIdentity("5.0.0", "hl7.fhir.r5.core", "5.0.0", "r5-policy-v1", "sdk-r5-v1");
        var provider = R5ModelMetadataProvider.Default;
        // Follow explicitly registered metadata and declared CLR properties, never discover resource factories by assembly scan.
        var types = new HashSet<Type>();
        var visited = new HashSet<Type>();
        var queue = new Queue<Type>(provider.Models.ResourceTypes.Concat(provider.ConcreteDataTypes)
            .Concat(PrimitiveRegistry.Default.Definitions.Select(p => p.PrimitiveType))
            .Concat(provider.ValidationRules.Entries.Keys));
        while (queue.TryDequeue(out var type))
        {
            if (!typeof(FhirObject).IsAssignableFrom(type) || !visited.Add(type)) continue;
            if (type.Assembly != typeof(FhirObject).Assembly) types.Add(type);
            foreach (var property in FhirJsonConventions.GetSerializableProperties(type))
            {
                var child = property.PropertyType;
                if (child.IsGenericType && child.GetGenericArguments().Length == 1)
                    child = child.GetGenericArguments()[0];
                queue.Enqueue(child);
            }
        }
        types.Remove(typeof(Meta)); types.Remove(typeof(Narrative)); types.Remove(typeof(Extension));
        var metadata = provider.Models.Export(identity, types);
        var primitives = new PrimitiveProviderDefinition(identity, PrimitiveRegistry.Default.Definitions.Select(p =>
            new ModelContracts.PrimitiveDefinition(p.FhirTypeName, p.PrimitiveType, p.ValueType, p.Codec, p.Validator)));
        var rules = provider.ValidationRules.Entries.SelectMany(entry => entry.Value.Select((rule, index) =>
            new ValidationRuleDefinition(entry.Key, $"{entry.Key.FullName}:{index}", node =>
            {
                var issues = new List<ValidationIssue>();
                rule.Validate(new FhirObjectGraphNode(node.Value, node.Path), issues);
                return issues.Select(i => new ModelValidationIssue(i.Path, i.Message,
                    (ModelIssueCode)i.Code, (ModelIssueSeverity)i.Severity, i.RuleId)).ToArray();
            })));
        return new FhirModelContext(metadata, primitives, new ValidationProviderDefinition(identity,
            new ModelCapabilities(true, true, true, false, false), rules));
    }
}
