using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class PrimitiveTypeMappingView
{
    private readonly IReadOnlyDictionary<string, PrimitiveTypeMapping> _mappings;
    private readonly IReadOnlyList<PrimitiveTypeMapping> _orderedMappings;

    public PrimitiveTypeMappingView(
        ValidatedPrimitiveGenerationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var orderedMappings = policy.Primitives
            .Where(primitive => primitive.IsSupported)
            .Select(primitive => new PrimitiveTypeMapping(
                primitive.FhirTypeName,
                primitive.WrapperName!,
                policy.PrimitiveNamespace))
            .OrderBy(mapping => mapping.FhirTypeName, StringComparer.Ordinal)
            .ToArray();
        var mappings = orderedMappings
            .ToDictionary(
                mapping => mapping.FhirTypeName,
                StringComparer.Ordinal);
        _mappings = new ReadOnlyDictionary<string, PrimitiveTypeMapping>(
            mappings);
        _orderedMappings = Array.AsReadOnly(orderedMappings);
    }

    public IReadOnlyList<PrimitiveTypeMapping> Mappings => _orderedMappings;

    public bool TryGet(
        string fhirTypeName,
        [NotNullWhen(true)] out PrimitiveTypeMapping? mapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirTypeName);
        return _mappings.TryGetValue(fhirTypeName, out mapping);
    }
}

public sealed record PrimitiveTypeMapping(
    string FhirTypeName,
    string WrapperName,
    string Namespace);
