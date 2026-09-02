using MyFhirSdk.CodeGen.Definitions;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed record DefinitionInventoryItem(
    string SourceIdentity,
    string Id,
    string FhirTypeName,
    string Canonical,
    string? DefinitionVersion,
    string FhirVersion,
    string Kind,
    bool IsAbstract,
    string? BaseDefinition,
    string? Derivation,
    DefinitionInventoryCategory Category,
    StructureDefinitionDto Definition);
