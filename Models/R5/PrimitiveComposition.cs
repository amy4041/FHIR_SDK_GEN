global using PrimitiveCodecs = MyFhirSdk.ModelSupport.PrimitiveCodecs;
global using PrimitiveValidators = MyFhirSdk.ModelSupport.PrimitiveValidators;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelSupport;

namespace MyFhirSdk.Primitives;

// This partial and the generated partial have the same Models compile owner.
internal sealed partial class PrimitiveRegistry
{
    internal static IReadOnlyList<PrimitiveDefinition> Definitions { get; } = Create();
    private static IReadOnlyList<PrimitiveDefinition> Create()
    {
        var definitions = new List<IPrimitiveDefinition>();
        AddGeneratedDefinitions(definitions);
        return Array.AsReadOnly(definitions.Select(d => d.Definition).ToArray());
    }
    static partial void AddGeneratedDefinitions(List<IPrimitiveDefinition> definitions);
    private static IPrimitiveDefinition Define<TPrimitive, TValue>(string name, IPrimitiveCodec codec, IPrimitiveValidator validator)
        => new(new PrimitiveDefinition(name, typeof(TPrimitive), typeof(TValue), codec, validator));
}

internal sealed record IPrimitiveDefinition(PrimitiveDefinition Definition);
