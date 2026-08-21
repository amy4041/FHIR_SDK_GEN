namespace MyFhirSdk.CodeGen.Inventory;

public sealed record PrimitiveDefinitionInventoryItem(
    string SourceFile,
    string FhirTypeName,
    string Canonical,
    string FhirVersion,
    string BaseDefinition,
    string DefinitionName,
    string? Description);
