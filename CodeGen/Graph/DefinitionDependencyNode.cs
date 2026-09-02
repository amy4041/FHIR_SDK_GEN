using MyFhirSdk.CodeGen.Inventory;

namespace MyFhirSdk.CodeGen.Graph;

public sealed record DefinitionDependencyNode(
    string Canonical,
    string FhirTypeName,
    string Kind,
    DefinitionInventoryCategory Category,
    DefinitionDependencyNodeDisposition Disposition,
    DefinitionInventoryItem InventoryItem,
    string? ExternalClrType = null);
