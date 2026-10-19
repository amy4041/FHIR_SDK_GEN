using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.R4.Primitives;

namespace MyFhirSdk.ModelMetadata.R4;

/// <summary>R4 model-owned provider data. Has no SDK or engines dependency.</summary>
public static class R4ModelProvider
{
    public static ModelIdentity Identity { get; } = new("4.0.1", "hl7.fhir.r4.core", "4.0.1", "r4-policy-v1", "r4-models-v1");
    public static PrimitiveProviderDefinition Primitives { get; } = new(Identity, PrimitiveRegistry.Definitions);
    public static ValidationProviderDefinition Validation { get; } = new(Identity,
        new ModelCapabilities(true, true, true, false, false), GeneratedR4ValidationRules.Create().Definitions);
    public static ModelMetadataDefinition Metadata { get; } = GeneratedR4ModelMetadata.Create().Export(Identity,
        typeof(R4ModelProvider).Assembly.GetExportedTypes()
            .Where(t => typeof(FhirObject).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal));
}
