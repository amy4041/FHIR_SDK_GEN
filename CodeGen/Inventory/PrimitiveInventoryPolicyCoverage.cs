using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class PrimitiveInventoryPolicyCoverage
{
    internal PrimitiveInventoryPolicyCoverage(
        PrimitiveDefinitionInventory inventory,
        ValidatedPrimitiveGenerationPolicy policy,
        IEnumerable<PrimitiveInventoryPolicyMatch> matches)
    {
        Inventory = inventory;
        Policy = policy;
        Matches = new ReadOnlyCollection<PrimitiveInventoryPolicyMatch>(
            matches.ToArray());
    }

    public PrimitiveDefinitionInventory Inventory { get; }

    public ValidatedPrimitiveGenerationPolicy Policy { get; }

    public IReadOnlyList<PrimitiveInventoryPolicyMatch> Matches { get; }
}

public sealed record PrimitiveInventoryPolicyMatch(
    PrimitiveDefinitionInventoryItem Definition,
    ValidatedPrimitivePolicyEntry Policy);
