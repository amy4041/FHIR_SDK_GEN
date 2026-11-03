using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.ModelMetadata.R5;

/// <summary>R5 model-owned provider data. Has no SDK or engines dependency.</summary>
public static class R5ModelProvider
{
    public static ModelIdentity Identity { get; } = new("5.0.0", "hl7.fhir.r5.core", "5.0.0", "r5-policy-v1", "r5-models-v1");
    public static PrimitiveProviderDefinition Primitives { get; } = new(Identity, PrimitiveRegistry.Definitions);
    public static ValidationProviderDefinition Validation { get; } = new(Identity,
        new ModelCapabilities(true, true, true, false, false), GeneratedR5ValidationRules.Create().Definitions);
    public static ModelMetadataDefinition Metadata { get; } = CreateMetadata();

    private static ModelMetadataDefinition CreateMetadata()
    {
        var data = GeneratedR5ModelMetadata.Create();
        var types = new HashSet<Type>();
        var visited = new HashSet<Type>();
        var queue = new Queue<Type>(data.Resources.Select(r => r.Type).Concat(data.DataTypes)
            .Concat(Primitives.Definitions.Select(p => p.PrimitiveType)).Concat(Validation.Rules.Select(r => r.AppliesTo))
            .Concat(new[] { typeof(Resources.CanonicalResource), typeof(Resources.MetadataResource) })
            .Concat(data.Extensions.Select(e => e.ValueType)).Concat(data.OpenTypes.Select(o => o.ValueType)));
        while (queue.TryDequeue(out var type))
        {
            if (!typeof(FhirObject).IsAssignableFrom(type) || !visited.Add(type)) continue;
            if (type.Assembly != typeof(FhirObject).Assembly) types.Add(type);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
            {
                var child = property.PropertyType;
                if (child.IsGenericType && child.GetGenericArguments().Length == 1) child = child.GetGenericArguments()[0];
                queue.Enqueue(child);
            }
        }
        return data.Export(Identity, types.OrderBy(t => t.FullName, StringComparer.Ordinal));
    }
}
