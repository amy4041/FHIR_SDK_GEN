using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Loading;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class DefinitionInventory
{
    internal DefinitionInventory(
        DefinitionPackageIdentity packageIdentity,
        IEnumerable<DefinitionInventoryItem> items)
    {
        ArgumentNullException.ThrowIfNull(packageIdentity);
        ArgumentNullException.ThrowIfNull(items);

        PackageIdentity = packageIdentity;
        Items = new ReadOnlyCollection<DefinitionInventoryItem>(items.ToArray());
    }

    public DefinitionPackageIdentity PackageIdentity { get; }

    public IReadOnlyList<DefinitionInventoryItem> Items { get; }
}
