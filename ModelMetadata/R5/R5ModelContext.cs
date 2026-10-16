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

    private static FhirModelContext Create() => new(R5ModelProvider.Metadata, R5ModelProvider.Primitives, R5ModelProvider.Validation);
}
