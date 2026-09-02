using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Graph;

public sealed class GenerationScope
{
    internal GenerationScope(
        IEnumerable<string> seedCanonicals,
        IEnumerable<DefinitionDependencyNode> generatedModels,
        IEnumerable<DefinitionDependencyNode> externalDependencies,
        IEnumerable<DefinitionDependencyNode> primitiveDependencies,
        IEnumerable<DefinitionDependencyEdge> traversedEdges)
    {
        SeedCanonicals = Array.AsReadOnly(seedCanonicals.ToArray());
        GeneratedModels = Array.AsReadOnly(generatedModels.ToArray());
        ExternalDependencies = Array.AsReadOnly(externalDependencies.ToArray());
        PrimitiveDependencies = Array.AsReadOnly(primitiveDependencies.ToArray());
        TraversedEdges = Array.AsReadOnly(traversedEdges.ToArray());
        GenerationPlan = new ReadOnlyCollection<GenerationPlanItem>(
            GeneratedModels
                .Select((node, index) => new GenerationPlanItem(
                    index,
                    node.Canonical,
                    node.FhirTypeName,
                    node.InventoryItem.SourceIdentity))
                .ToArray());
    }

    public IReadOnlyList<string> SeedCanonicals { get; }

    public IReadOnlyList<DefinitionDependencyNode> GeneratedModels { get; }

    public IReadOnlyList<DefinitionDependencyNode> ExternalDependencies { get; }

    public IReadOnlyList<DefinitionDependencyNode> PrimitiveDependencies { get; }

    public IReadOnlyList<DefinitionDependencyEdge> TraversedEdges { get; }

    public IReadOnlyList<GenerationPlanItem> GenerationPlan { get; }
}

public sealed record GenerationPlanItem(
    int Ordinal,
    string Canonical,
    string FhirTypeName,
    string SourceIdentity);
