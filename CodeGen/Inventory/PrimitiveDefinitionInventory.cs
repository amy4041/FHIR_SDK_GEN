using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class PrimitiveDefinitionInventory
{
    internal PrimitiveDefinitionInventory(
        string fhirVersion,
        IEnumerable<PrimitiveDefinitionInventoryItem> items)
    {
        FhirVersion = fhirVersion;
        Items = new ReadOnlyCollection<PrimitiveDefinitionInventoryItem>(
            items.ToArray());
    }

    public string FhirVersion { get; }

    public IReadOnlyList<PrimitiveDefinitionInventoryItem> Items { get; }
}
