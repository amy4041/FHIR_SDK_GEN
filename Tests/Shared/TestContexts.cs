using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;

namespace MyFhirSdk.Tests.Shared;

// Test-only release selection; production engines have no implicit model version.
internal static class TestContexts
{
    internal static FhirModelContext R5 { get; } = new(
        R5ModelProvider.Metadata, R5ModelProvider.Primitives, R5ModelProvider.Validation);
}
