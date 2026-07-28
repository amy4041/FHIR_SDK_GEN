using MyFhirSdk.CodeGen.Definitions;

namespace MyFhirSdk.CodeGen.Loading;

public sealed record LoadedStructureDefinition(
    string SourceFile,
    StructureDefinitionDto Definition);
