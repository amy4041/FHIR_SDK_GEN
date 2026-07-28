namespace MyFhirSdk.CodeGen.Diagnostics;

public sealed record GeneratorDiagnostic(
    string Code,
    GeneratorDiagnosticSeverity Severity,
    string Message,
    string SourceFile,
    string? DefinitionCanonical = null,
    string? DefinitionVersion = null,
    string? ElementId = null,
    string? ElementPath = null);
