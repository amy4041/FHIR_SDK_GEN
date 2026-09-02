using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Loading;

namespace MyFhirSdk.CodeGen.Graph;

public sealed class DefinitionDependencyGraph
{
    private readonly IReadOnlyDictionary<string, DefinitionDependencyNode> _nodesByCanonical;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<DefinitionDependencyEdge>> _outgoingEdges;

    internal DefinitionDependencyGraph(
        DefinitionPackageIdentity packageIdentity,
        IEnumerable<DefinitionDependencyNode> nodes,
        IEnumerable<DefinitionDependencyEdge> edges)
    {
        PackageIdentity = packageIdentity;
        Nodes = Array.AsReadOnly(nodes.ToArray());
        Edges = Array.AsReadOnly(edges.ToArray());
        _nodesByCanonical = new ReadOnlyDictionary<string, DefinitionDependencyNode>(
            Nodes.ToDictionary(node => node.Canonical, StringComparer.Ordinal));
        _outgoingEdges = new ReadOnlyDictionary<string, IReadOnlyList<DefinitionDependencyEdge>>(
            Edges
                .GroupBy(edge => edge.SourceCanonical, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<DefinitionDependencyEdge>)Array.AsReadOnly(group.ToArray()),
                    StringComparer.Ordinal));
    }

    public DefinitionPackageIdentity PackageIdentity { get; }

    public IReadOnlyList<DefinitionDependencyNode> Nodes { get; }

    public IReadOnlyList<DefinitionDependencyEdge> Edges { get; }

    public bool TryGetNode(string canonical, out DefinitionDependencyNode? node) =>
        _nodesByCanonical.TryGetValue(canonical, out node);

    public IReadOnlyList<DefinitionDependencyEdge> GetOutgoingEdges(string canonical) =>
        _outgoingEdges.TryGetValue(canonical, out var edges)
            ? edges
            : Array.Empty<DefinitionDependencyEdge>();
}
