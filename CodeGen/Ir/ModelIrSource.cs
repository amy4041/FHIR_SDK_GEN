namespace MyFhirSdk.CodeGen.Ir;

public sealed record ModelIrSource(
    string SourceIdentity,
    string DefinitionCanonical,
    string? DefinitionVersion,
    string? ElementId = null,
    string? ElementPath = null);
