using MyFhirSdk.Core;

namespace MyFhirSdk.Validation.Traversal;

internal readonly record struct FhirObjectGraphNode(FhirObject Value, string Path);
