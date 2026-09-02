using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Graph;

public sealed class GenerationScopeSelector
{
    public GenerationResult<GenerationScope?> SelectAll(
        DefinitionDependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Select(
            graph,
            graph.Nodes
                .Where(node => node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel)
                .Select(node => node.Canonical));
    }

    public GenerationResult<GenerationScope?> Select(
        DefinitionDependencyGraph graph,
        IEnumerable<string> selectedCanonicals)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(selectedCanonicals);

        var seeds = selectedCanonicals
            .Where(canonical => !string.IsNullOrWhiteSpace(canonical))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(canonical => canonical, StringComparer.Ordinal)
            .ToArray();
        var diagnostics = new List<GeneratorDiagnostic>();
        var queue = new Queue<DefinitionDependencyNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var generated = new Dictionary<string, DefinitionDependencyNode>(StringComparer.Ordinal);
        var external = new Dictionary<string, DefinitionDependencyNode>(StringComparer.Ordinal);
        var primitives = new Dictionary<string, DefinitionDependencyNode>(StringComparer.Ordinal);
        var traversedEdges = new HashSet<DefinitionDependencyEdge>();

        foreach (var canonical in seeds)
        {
            if (!graph.TryGetNode(canonical, out var node) || node is null ||
                node.Disposition != DefinitionDependencyNodeDisposition.GeneratedModel)
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidGenerationScope,
                    GeneratorDiagnosticSeverity.Error,
                    $"Selected canonical '{canonical}' is not a generated model node.",
                    "<generation-scope>",
                    canonical));
                continue;
            }

            queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            var source = queue.Dequeue();
            if (!visited.Add(source.Canonical))
            {
                continue;
            }

            generated[source.Canonical] = source;
            foreach (var edge in graph.GetOutgoingEdges(source.Canonical))
            {
                traversedEdges.Add(edge);
                if (!graph.TryGetNode(edge.TargetCanonical, out var target) || target is null)
                {
                    diagnostics.Add(new GeneratorDiagnostic(
                        GeneratorDiagnosticCodes.MissingDependency,
                        GeneratorDiagnosticSeverity.Error,
                        $"Graph edge target '{edge.TargetCanonical}' is missing.",
                        source.InventoryItem.SourceIdentity,
                        source.Canonical,
                        source.InventoryItem.DefinitionVersion,
                        edge.SourceElementId));
                    continue;
                }

                switch (target.Disposition)
                {
                    case DefinitionDependencyNodeDisposition.GeneratedModel:
                        if (!visited.Contains(target.Canonical))
                        {
                            queue.Enqueue(target);
                        }
                        break;
                    case DefinitionDependencyNodeDisposition.ExternalHandwritten:
                        external[target.Canonical] = target;
                        break;
                    case DefinitionDependencyNodeDisposition.SupportedPrimitive:
                        primitives[target.Canonical] = target;
                        break;
                    case DefinitionDependencyNodeDisposition.UnsupportedPrimitive:
                        diagnostics.Add(new GeneratorDiagnostic(
                            GeneratorDiagnosticCodes.UnsupportedPrimitiveReference,
                            GeneratorDiagnosticSeverity.Error,
                            $"Element dependency '{edge.ReferenceIdentity}' resolves to unsupported primitive '{target.FhirTypeName}'.",
                            source.InventoryItem.SourceIdentity,
                            source.Canonical,
                            source.InventoryItem.DefinitionVersion,
                            edge.SourceElementId));
                        break;
                    case DefinitionDependencyNodeDisposition.ConstraintProfile:
                    case DefinitionDependencyNodeDisposition.LogicalModel:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        if (diagnostics.Count > 0)
        {
            return new GenerationResult<GenerationScope?>(
                null,
                DefinitionDependencyGraphBuilder.OrderDiagnostics(diagnostics));
        }

        return new GenerationResult<GenerationScope?>(
            new GenerationScope(
                seeds,
                generated.Values.OrderBy(node => node.Canonical, StringComparer.Ordinal),
                external.Values.OrderBy(node => node.Canonical, StringComparer.Ordinal),
                primitives.Values.OrderBy(node => node.Canonical, StringComparer.Ordinal),
                traversedEdges
                    .OrderBy(edge => edge.SourceCanonical, StringComparer.Ordinal)
                    .ThenBy(edge => edge.SourceElementId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.Kind)
                    .ThenBy(edge => edge.TargetCanonical, StringComparer.Ordinal)
                    .ThenBy(edge => edge.TargetElementId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.ReferenceIdentity, StringComparer.Ordinal)),
            Array.Empty<GeneratorDiagnostic>());
    }
}
