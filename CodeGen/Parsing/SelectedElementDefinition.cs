using MyFhirSdk.CodeGen.Definitions;

namespace MyFhirSdk.CodeGen.Parsing;

public sealed record SelectedElementDefinition(
    ElementDefinitionDto DifferentialElement,
    ElementDefinitionDto SnapshotElement,
    int Order);
