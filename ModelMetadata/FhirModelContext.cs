using System.Collections;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Rules;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.ModelMetadata;

/// <summary>Immutable composition of one release's models and providers. No global current version.</summary>
public sealed class FhirModelContext
{
    private readonly HashSet<Type> _modelTypes;
    internal IModelMetadataProvider Metadata { get; }
    internal PrimitiveRegistry Primitives { get; }
    internal IValidationRuleProvider Rules { get; }

    public FhirModelContext(ModelMetadataDefinition metadata, PrimitiveProviderDefinition primitives,
        ValidationProviderDefinition validation)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(validation);
        Identity = metadata.Identity;
        if (Identity != primitives.Identity || Identity != validation.Identity)
            throw new FhirSdkException("FHIR_CONTEXT_IDENTITY: model, primitive and validation identities must match.");
        Capabilities = validation.Capabilities;
        _modelTypes = metadata.ModelTypes.ToHashSet();
        if (_modelTypes.Count != metadata.ModelTypes.Count || _modelTypes.Count == 0)
            throw new FhirSdkException("FHIR_CONTEXT_MODELS: model inventory must be nonempty and unique.");
        foreach (var type in _modelTypes)
            if (!typeof(FhirObject).IsAssignableFrom(type) || type.IsInterface || type.Assembly == typeof(FhirObject).Assembly)
                throw new FhirSdkException($"FHIR_CONTEXT_MODELS: invalid model declaration '{type}'.");
        void Owned(Type type)
        {
            if (!_modelTypes.Contains(type) && !IsShared(type))
                throw new FhirSdkException($"FHIR_CONTEXT_PROVIDER: '{type}' is outside the model inventory.");
        }
        void DeclaringOwned(Type type)
        {
            if (type.IsAbstract && typeof(FhirObject).IsAssignableFrom(type) &&
                _modelTypes.Any(type.IsAssignableFrom)) return;
            Owned(type);
        }
        foreach (var resource in metadata.Resources)
        {
            Owned(resource.ResourceType);
            ArgumentNullException.ThrowIfNull(resource.Factory);
        }
        foreach (var type in metadata.ConcreteDataTypes) Owned(type);
        foreach (var entry in metadata.DeclaredDataTypes) { DeclaringOwned(entry.DeclaringType); Owned(entry.ConcreteType); }
        foreach (var entry in metadata.ExtensionValues) Owned(entry.ValueType);
        foreach (var entry in metadata.OpenTypes) { DeclaringOwned(entry.DeclaringType); Owned(entry.ValueType); }
        foreach (var primitive in primitives.Definitions) Owned(primitive.PrimitiveType);
        foreach (var rule in validation.Rules)
        {
            DeclaringOwned(rule.AppliesTo);
            ArgumentException.ThrowIfNullOrWhiteSpace(rule.RuleId);
            ArgumentNullException.ThrowIfNull(rule.Evaluate);
        }
        foreach (var type in _modelTypes.Where(t => typeof(IPrimitiveValueAccessor).IsAssignableFrom(t)))
            if (!primitives.Definitions.Any(p => p.PrimitiveType == type))
                throw new FhirSdkException($"FHIR_CONTEXT_PROVIDER: missing primitive '{type}'.");
        foreach (var type in _modelTypes.Where(t => !t.IsAbstract && typeof(Resource).IsAssignableFrom(t)))
            if (!metadata.Resources.Any(r => r.ResourceType == type))
                throw new FhirSdkException($"FHIR_CONTEXT_PROVIDER: missing resource factory '{type}'.");

        Metadata = new ImmutableModelMetadataProvider(
            metadata.Resources.Select(r => new ResourceTypeMetadata(r.FhirTypeName, r.ResourceType, () =>
            {
                var value = r.Factory();
                if (value is null || value.GetType() != r.ResourceType || value.ResourceType != r.FhirTypeName)
                    throw new FhirSdkException("FHIR_CONTEXT_FACTORY: factory must return its exact declared CLR/FHIR type.");
                return value;
            })), metadata.ConcreteDataTypes,
            metadata.DeclaredDataTypes.Select(d => new DeclaredDataTypeMetadata(d.DeclaringType, d.PropertyName, d.ConcreteType)),
            metadata.ExtensionValues.Select(e => new ExtensionValueMetadata(e.ValueType, e.PropertyName, e.IsParserTarget)),
            metadata.OpenTypes.Select(o => new OpenTypeValueMetadata(o.DeclaringType, o.PropertyName, o.ValueType, o.JsonPropertyName)));
        var coding = metadata.ExtensionValues.SingleOrDefault(e => e.IsParserTarget && e.PropertyName == "valueCoding");
        foreach (var property in new[] { "tag", "security" })
            if (coding is null || !Metadata.TryGetDeclaredDataType(typeof(Meta), property, out var declared) ||
                declared != coding.ValueType)
                throw new FhirSdkException($"FHIR_CONTEXT_PROVIDER: Meta.{property} must declare this context's Coding type.");
        Primitives = PrimitiveRegistry.Create(primitives.Definitions.Select(p =>
            new Primitives.PrimitiveDefinition(p.FhirTypeName, p.PrimitiveType, p.ValueType,
                new CodecAdapter(p.Codec), new ValidatorAdapter(p.Validator))));
        // Every context needs the shared string id validator, including for bootstrap nodes.
        Primitives.GetRequired("id");
        Rules = new RuleProvider(validation.Rules);
    }

    public ModelIdentity Identity { get; }
    public ModelCapabilities Capabilities { get; }
    public Resource CreateResource(string fhirTypeName)
    {
        var value = Metadata.GetRequiredResource(fhirTypeName).CreateResource();
        EnsureOwnership(value);
        return value;
    }

    internal void EnsureOwnership(FhirObject root)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(root, root is Resource r ? r.ResourceType : root.GetType().Name);
        void Reject(Type type, string path) => throw new FhirSdkException(
            $"FHIR_MODEL_OWNERSHIP: context {Identity.FhirVersion} rejects '{type.FullName}' at {path}.");
        void Visit(object? value, string path)
        {
            if (value is null || value is string || !visited.Add(value)) return;
            // A FHIR model can implement IEnumerable; it remains a model node,
            // and must never bypass the exact CLR ownership check as a collection.
            if (value is not FhirObject && value is IEnumerable list)
            {
                var index = 0;
                foreach (var item in list) Visit(item, $"{path}[{index++}]");
                return;
            }
            if (value is not FhirObject fhir) return;
            var type = fhir.GetType();
            if (!_modelTypes.Contains(type) && !IsShared(type)) Reject(type, path);
            if (fhir is IPrimitiveValueAccessor) Primitives.GetRequired(type);
            if (fhir is Resource) Metadata.GetRequiredResource(type);
            if (fhir is Extension { Value: not null } extension)
            {
                Visit(extension.Value, path + ".value");
                Metadata.GetRequiredExtensionValuePropertyName(extension.Value.GetType());
            }
            // Match the validator's object graph, including JsonIgnore properties.
            // JSON visibility does not change a model's context ownership.
            foreach (var property in FhirObjectGraphWalker.GetReadableProperties(type))
            {
                var child = property.GetValue(value);
                var name = FhirJsonConventions.GetJsonPropertyName(property);
                if (child is not null && Metadata.TryGetDeclaredDataType(property.DeclaringType ?? type, name, out var declared))
                {
                    if (child is not FhirObject && child is IEnumerable items)
                    {
                        var index = 0;
                        foreach (var item in items)
                        {
                            if (item is not null && item.GetType() != declared) Reject(item.GetType(), $"{path}.{name}[{index}]");
                            index++;
                        }
                    }
                    else if (child.GetType() != declared) Reject(child.GetType(), path + "." + name);
                }
                Visit(child, path + "." + name);
            }
        }
    }

    private static bool IsShared(Type type) => type == typeof(Meta) || type == typeof(Narrative) || type == typeof(Extension);

    private sealed class CodecAdapter(ModelContracts.IPrimitiveCodec codec) : Primitives.IPrimitiveCodec
    {
        private readonly ModelContracts.IPrimitiveCodec _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        public object CreatePrimitive(Type type, System.Text.Json.JsonElement? raw)
        {
            object result;
            try { result = _codec.CreatePrimitive(type, raw); }
            catch (ModelSupport.PrimitiveCodecException exception)
            {
                throw new FhirSdkException(exception.Message, exception);
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
            {
                throw new FhirSdkException($"FHIR_PRIMITIVE_CODEC: could not read '{type.FullName}'.", exception);
            }
            if (result is null || result.GetType() != type)
                throw new FhirSdkException("FHIR_CONTEXT_CODEC: codec returned an unexpected CLR type.");
            return result;
        }
        public bool HasRawValue(object value) => _codec.HasRawValue(value);
        public void WriteRawValue(System.Text.Json.Utf8JsonWriter writer, object value, bool missing) => _codec.WriteRawValue(writer, value, missing);
    }

    private sealed class ValidatorAdapter(ModelContracts.IPrimitiveValidator validator) : Primitives.IPrimitiveValidator
    {
        private readonly ModelContracts.IPrimitiveValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        public bool IsValid(object value) => _validator.IsValid(value);
        public bool IsValidValue(object? value) => _validator.IsValidValue(value);
    }

    private sealed class RuleProvider : IValidationRuleProvider
    {
        private readonly IReadOnlyDictionary<Type, IFhirValidationRule[]> _rules;
        internal RuleProvider(IReadOnlyList<ValidationRuleDefinition> rules) => _rules = rules.GroupBy(r => r.AppliesTo)
            .ToDictionary(g => g.Key, g => g.Select(r => (IFhirValidationRule)new RuleAdapter(r)).ToArray());
        public IReadOnlyList<IFhirValidationRule> GetRules(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (var current = type; current is not null; current = current.BaseType) hierarchy.Push(current);
            var result = new List<IFhirValidationRule>();
            while (hierarchy.TryPop(out var current))
                if (_rules.TryGetValue(current, out var rules)) result.AddRange(rules);
            return result;
        }
    }

    private sealed class RuleAdapter(ValidationRuleDefinition rule) : IFhirValidationRule
    {
        public void Validate(FhirObjectGraphNode node, ICollection<ValidationIssue> issues)
        {
            foreach (var issue in rule.Evaluate(new ModelValidationNode(node.Value, node.Path)))
                issues.Add(new ValidationIssue { Path = issue.Path, Message = issue.Message,
                    Code = (ValidationIssueCode)issue.Code, Severity = (ValidationSeverity)issue.Severity,
                    RuleId = issue.RuleId });
        }
    }
}
