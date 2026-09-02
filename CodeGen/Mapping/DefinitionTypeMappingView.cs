using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using MyFhirSdk.CodeGen.Graph;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class DefinitionTypeMappingView
{
    private const string ResourceNamespace = "MyFhirSdk.Resources";
    private const string TypeNamespace = "MyFhirSdk.Types";

    private readonly IReadOnlyDictionary<string, DefinitionTypeMapping> _mappings;

    public DefinitionTypeMappingView(IEnumerable<DefinitionTypeMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        _mappings = new ReadOnlyDictionary<string, DefinitionTypeMapping>(
            mappings.ToDictionary(
                mapping => mapping.FhirTypeName,
                StringComparer.Ordinal));
    }

    public static DefinitionTypeMappingView FromGraph(DefinitionDependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new DefinitionTypeMappingView(graph.Nodes
            .Where(node => node.Disposition is
                DefinitionDependencyNodeDisposition.GeneratedModel or
                DefinitionDependencyNodeDisposition.ExternalHandwritten)
            .Select(CreateMapping)
            .OrderBy(mapping => mapping.FhirTypeName, StringComparer.Ordinal));
    }

    public bool TryGet(
        string fhirTypeName,
        [NotNullWhen(true)] out DefinitionTypeMapping? mapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirTypeName);
        return _mappings.TryGetValue(fhirTypeName, out mapping);
    }

    private static DefinitionTypeMapping CreateMapping(DefinitionDependencyNode node)
    {
        if (node.Disposition == DefinitionDependencyNodeDisposition.ExternalHandwritten)
        {
            var clrType = node.ExternalClrType!;
            var separator = clrType.LastIndexOf('.');
            if (separator <= 0 || separator == clrType.Length - 1)
            {
                throw new InvalidOperationException(
                    $"External definition '{node.Canonical}' has invalid CLR type '{clrType}'.");
            }

            return new DefinitionTypeMapping(
                node.FhirTypeName,
                clrType[(separator + 1)..],
                clrType[..separator]);
        }

        return new DefinitionTypeMapping(
            node.FhirTypeName,
            node.FhirTypeName,
            string.Equals(node.Kind, "resource", StringComparison.Ordinal)
                ? ResourceNamespace
                : TypeNamespace);
    }
}

public sealed record DefinitionTypeMapping(
    string FhirTypeName,
    string TypeName,
    string Namespace);
