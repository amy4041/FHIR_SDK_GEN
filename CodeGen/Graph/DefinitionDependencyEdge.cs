namespace MyFhirSdk.CodeGen.Graph;

public sealed record DefinitionDependencyEdge(
    string SourceCanonical,
    string? SourceElementId,
    DefinitionDependencyEdgeKind Kind,
    string TargetCanonical,
    string? TargetElementId,
    string ReferenceIdentity);
