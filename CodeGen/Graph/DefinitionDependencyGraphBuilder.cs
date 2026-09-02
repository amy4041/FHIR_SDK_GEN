using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Graph;

public sealed class DefinitionDependencyGraphBuilder
{
    private const string ExternalDisposition = "external-handwritten";

    public GenerationResult<DefinitionDependencyGraph?> Build(
        DefinitionInventory inventory,
        PrimitiveTypeMappingView primitiveMappings,
        ModelOwnershipPolicyDocument ownershipPolicy,
        string ownershipPolicySource)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(primitiveMappings);
        ArgumentNullException.ThrowIfNull(ownershipPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownershipPolicySource);

        var diagnostics = new List<GeneratorDiagnostic>();
        var externalByCanonical = ValidateExternalNodes(
            inventory,
            ownershipPolicy,
            ownershipPolicySource,
            diagnostics);

        var nodes = inventory.Items
            .OrderBy(item => item.Canonical, StringComparer.Ordinal)
            .Select(item => CreateNode(item, externalByCanonical, primitiveMappings))
            .ToArray();
        var nodesByCanonical = nodes.ToDictionary(
            node => node.Canonical,
            StringComparer.Ordinal);
        var typeNodes = nodes
            .Where(node => node.Category is DefinitionInventoryCategory.ModelRoot or
                DefinitionInventoryCategory.ModelSpecialization or
                DefinitionInventoryCategory.PrimitiveSpecialization)
            .ToDictionary(node => node.FhirTypeName, StringComparer.Ordinal);

        if (diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        var edges = new HashSet<DefinitionDependencyEdge>();
        AddInheritanceEdges(nodes, nodesByCanonical, edges, diagnostics);
        AddReferenceEdges(nodes, nodesByCanonical, typeNodes, edges, diagnostics);

        var orderedEdges = OrderEdges(edges).ToArray();
        ValidateInheritanceCycles(nodes, orderedEdges, diagnostics);
        if (diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        return new GenerationResult<DefinitionDependencyGraph?>(
            new DefinitionDependencyGraph(inventory.PackageIdentity, nodes, orderedEdges),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static IReadOnlyDictionary<string, ExternalDefinitionPolicyNode>
        ValidateExternalNodes(
            DefinitionInventory inventory,
            ModelOwnershipPolicyDocument policy,
            string policySource,
            ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (policy.SchemaVersion != 1 ||
            !string.Equals(policy.FhirVersion, inventory.PackageIdentity.FhirVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidDependencyGraph,
                GeneratorDiagnosticSeverity.Error,
                "The model ownership policy must use schema version 1 and match the inventory FHIR version.",
                policySource,
                DefinitionVersion: policy.FhirVersion));
        }

        var inventoryByCanonical = inventory.Items.ToDictionary(
            item => item.Canonical,
            StringComparer.Ordinal);
        var result = new Dictionary<string, ExternalDefinitionPolicyNode>(StringComparer.Ordinal);
        foreach (var external in (policy.ExternalDefinitionNodes ?? [])
            .OrderBy(node => node.Canonical, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(external.Canonical) ||
                string.IsNullOrWhiteSpace(external.FhirType) ||
                string.IsNullOrWhiteSpace(external.Kind) ||
                string.IsNullOrWhiteSpace(external.ClrType) ||
                !string.Equals(external.GenerationDisposition, ExternalDisposition, StringComparison.Ordinal))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidDependencyGraph,
                    GeneratorDiagnosticSeverity.Error,
                    "Every external definition node must provide canonical, FHIR type, kind, CLR type, and the external-handwritten disposition.",
                    policySource,
                    external.Canonical));
                continue;
            }

            if (!result.TryAdd(external.Canonical, external))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidDependencyGraph,
                    GeneratorDiagnosticSeverity.Error,
                    $"External definition canonical '{external.Canonical}' is duplicated.",
                    policySource,
                    external.Canonical));
                continue;
            }

            if (!inventoryByCanonical.TryGetValue(external.Canonical, out var item))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.MissingDependency,
                    GeneratorDiagnosticSeverity.Error,
                    $"Approved external definition '{external.Canonical}' is absent from the definition inventory.",
                    policySource,
                    external.Canonical));
                continue;
            }

            if (!string.Equals(item.FhirTypeName, external.FhirType, StringComparison.Ordinal) ||
                !string.Equals(item.Kind, external.Kind, StringComparison.Ordinal) ||
                item.IsAbstract != external.IsAbstract ||
                !string.Equals(item.BaseDefinition, external.BaseCanonical, StringComparison.Ordinal))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidDependencyGraph,
                    GeneratorDiagnosticSeverity.Error,
                    $"External definition '{external.Canonical}' does not match its inventory identity.",
                    policySource,
                    external.Canonical,
                    inventory.PackageIdentity.FhirVersion));
            }
        }

        return result;
    }

    private static DefinitionDependencyNode CreateNode(
        DefinitionInventoryItem item,
        IReadOnlyDictionary<string, ExternalDefinitionPolicyNode> externalByCanonical,
        PrimitiveTypeMappingView primitiveMappings)
    {
        if (externalByCanonical.TryGetValue(item.Canonical, out var external))
        {
            return new DefinitionDependencyNode(
                item.Canonical,
                item.FhirTypeName,
                item.Kind,
                item.Category,
                DefinitionDependencyNodeDisposition.ExternalHandwritten,
                item,
                external.ClrType);
        }

        var disposition = item.Category switch
        {
            DefinitionInventoryCategory.ModelRoot or
                DefinitionInventoryCategory.ModelSpecialization =>
                DefinitionDependencyNodeDisposition.GeneratedModel,
            DefinitionInventoryCategory.PrimitiveSpecialization =>
                primitiveMappings.TryGet(item.FhirTypeName, out _)
                    ? DefinitionDependencyNodeDisposition.SupportedPrimitive
                    : DefinitionDependencyNodeDisposition.UnsupportedPrimitive,
            DefinitionInventoryCategory.ConstraintProfile =>
                DefinitionDependencyNodeDisposition.ConstraintProfile,
            DefinitionInventoryCategory.LogicalModel =>
                DefinitionDependencyNodeDisposition.LogicalModel,
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

        return new DefinitionDependencyNode(
            item.Canonical,
            item.FhirTypeName,
            item.Kind,
            item.Category,
            disposition,
            item);
    }

    private static void AddInheritanceEdges(
        IEnumerable<DefinitionDependencyNode> nodes,
        IReadOnlyDictionary<string, DefinitionDependencyNode> nodesByCanonical,
        ISet<DefinitionDependencyEdge> edges,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var node in nodes.Where(node => node.Category == DefinitionInventoryCategory.ModelSpecialization))
        {
            var baseCanonical = node.InventoryItem.BaseDefinition;
            if (string.IsNullOrWhiteSpace(baseCanonical) ||
                !nodesByCanonical.TryGetValue(baseCanonical, out var baseNode))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.MissingDependency,
                    node,
                    $"Base definition '{baseCanonical ?? "<missing>"}' cannot be resolved by canonical identity."));
                continue;
            }

            if (baseNode.Category is not (DefinitionInventoryCategory.ModelRoot or
                DefinitionInventoryCategory.ModelSpecialization) ||
                !IsCompatibleBase(node, baseNode))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.IncompatibleInheritance,
                    node,
                    $"FHIR kind '{node.Kind}' cannot derive from '{baseNode.Kind}' at '{baseNode.Canonical}'."));
                continue;
            }

            edges.Add(new DefinitionDependencyEdge(
                node.Canonical,
                null,
                DefinitionDependencyEdgeKind.Inheritance,
                baseNode.Canonical,
                null,
                baseCanonical));
        }
    }

    private static bool IsCompatibleBase(
        DefinitionDependencyNode node,
        DefinitionDependencyNode baseNode) =>
        string.Equals(node.Kind, baseNode.Kind, StringComparison.Ordinal) ||
        string.Equals(node.Kind, "resource", StringComparison.Ordinal) &&
        string.Equals(node.FhirTypeName, "Resource", StringComparison.Ordinal) &&
        string.Equals(baseNode.FhirTypeName, "Base", StringComparison.Ordinal);

    private static void AddReferenceEdges(
        IEnumerable<DefinitionDependencyNode> nodes,
        IReadOnlyDictionary<string, DefinitionDependencyNode> nodesByCanonical,
        IReadOnlyDictionary<string, DefinitionDependencyNode> typeNodes,
        ISet<DefinitionDependencyEdge> edges,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var node in nodes.Where(node =>
            node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel))
        {
            var elements = node.InventoryItem.Definition.Snapshot?.Elements ?? [];
            var elementIds = elements
                .Where(element => !string.IsNullOrWhiteSpace(element.Id))
                .Select(element => element.Id!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var element in elements
                .Where(element => IsDirectElement(node, element))
                .OrderBy(element => element.Id, StringComparer.Ordinal))
            {
                AddContentReferenceEdge(node, element, elementIds, nodesByCanonical, edges, diagnostics);
                foreach (var type in element.Types ?? [])
                {
                    AddTypeEdge(node, element, type, typeNodes, edges, diagnostics);
                    AddCanonicalEdges(node, element, type.Profiles, DefinitionDependencyEdgeKind.Profile,
                        nodesByCanonical, edges, diagnostics);
                    AddCanonicalEdges(node, element, type.TargetProfiles, DefinitionDependencyEdgeKind.TargetProfile,
                        nodesByCanonical, edges, diagnostics);
                }
            }
        }
    }

    private static bool IsDirectElement(
        DefinitionDependencyNode node,
        ElementDefinitionDto element)
    {
        if (string.IsNullOrWhiteSpace(element.Id) ||
            string.Equals(element.Id, node.FhirTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        var basePath = element.Base?.Path;
        return string.IsNullOrWhiteSpace(basePath)
            ? element.Id.StartsWith(node.FhirTypeName + ".", StringComparison.Ordinal)
            : basePath.StartsWith(node.FhirTypeName + ".", StringComparison.Ordinal);
    }

    private static void AddTypeEdge(
        DefinitionDependencyNode source,
        ElementDefinitionDto element,
        ElementTypeDto type,
        IReadOnlyDictionary<string, DefinitionDependencyNode> typeNodes,
        ISet<DefinitionDependencyEdge> edges,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(type.Code) || !typeNodes.TryGetValue(type.Code, out var target))
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.MissingDependency,
                source,
                element,
                $"Element type '{type.Code ?? "<missing>"}' cannot be resolved by inventory type identity."));
            return;
        }

        edges.Add(new DefinitionDependencyEdge(
            source.Canonical,
            element.Id,
            DefinitionDependencyEdgeKind.ElementType,
            target.Canonical,
            null,
            type.Code));

        if (type.Code is "BackboneElement" or "BackboneType")
        {
            edges.Add(new DefinitionDependencyEdge(
                source.Canonical,
                element.Id,
                DefinitionDependencyEdgeKind.BackboneOwner,
                source.Canonical,
                element.Id,
                source.Canonical));
        }
    }

    private static void AddCanonicalEdges(
        DefinitionDependencyNode source,
        ElementDefinitionDto element,
        IEnumerable<string>? references,
        DefinitionDependencyEdgeKind kind,
        IReadOnlyDictionary<string, DefinitionDependencyNode> nodesByCanonical,
        ISet<DefinitionDependencyEdge> edges,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var reference in (references ?? []).OrderBy(value => value, StringComparer.Ordinal))
        {
            var canonical = NormalizeCanonical(reference);
            if (!nodesByCanonical.TryGetValue(canonical, out var target))
            {
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.MissingDependency,
                    source,
                    element,
                    $"{kind} reference '{reference}' cannot be resolved by canonical identity."));
                continue;
            }

            edges.Add(new DefinitionDependencyEdge(
                source.Canonical,
                element.Id,
                kind,
                target.Canonical,
                null,
                reference));
        }
    }

    private static void AddContentReferenceEdge(
        DefinitionDependencyNode source,
        ElementDefinitionDto element,
        IReadOnlySet<string> elementIds,
        IReadOnlyDictionary<string, DefinitionDependencyNode> nodesByCanonical,
        ISet<DefinitionDependencyEdge> edges,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(element.ContentReference))
        {
            return;
        }

        var reference = element.ContentReference;
        string targetCanonical;
        string targetElementId;
        if (reference.StartsWith('#'))
        {
            targetCanonical = source.Canonical;
            targetElementId = reference[1..];
        }
        else
        {
            var fragmentIndex = reference.IndexOf('#');
            targetCanonical = NormalizeCanonical(fragmentIndex < 0 ? reference : reference[..fragmentIndex]);
            targetElementId = fragmentIndex < 0 ? string.Empty : reference[(fragmentIndex + 1)..];
        }

        var targetExists = nodesByCanonical.TryGetValue(targetCanonical, out var targetNode) &&
            !string.IsNullOrWhiteSpace(targetElementId) &&
            (string.Equals(targetCanonical, source.Canonical, StringComparison.Ordinal)
                ? elementIds.Contains(targetElementId)
                : targetNode.InventoryItem.Definition.Snapshot?.Elements?.Any(
                    candidate => string.Equals(candidate.Id, targetElementId, StringComparison.Ordinal)) == true);
        if (!targetExists)
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.MissingDependency,
                source,
                element,
                $"contentReference '{reference}' cannot be resolved to a snapshot element."));
            return;
        }

        edges.Add(new DefinitionDependencyEdge(
            source.Canonical,
            element.Id,
            DefinitionDependencyEdgeKind.ContentReference,
            targetCanonical,
            targetElementId,
            reference));
    }

    private static string NormalizeCanonical(string reference)
    {
        var versionIndex = reference.IndexOf('|');
        return versionIndex < 0 ? reference : reference[..versionIndex];
    }

    private static void ValidateInheritanceCycles(
        IEnumerable<DefinitionDependencyNode> nodes,
        IEnumerable<DefinitionDependencyEdge> edges,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var bases = edges
            .Where(edge => edge.Kind == DefinitionDependencyEdgeKind.Inheritance)
            .ToDictionary(edge => edge.SourceCanonical, edge => edge.TargetCanonical, StringComparer.Ordinal);
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes
            .Where(node => node.Category is DefinitionInventoryCategory.ModelRoot or
                DefinitionInventoryCategory.ModelSpecialization)
            .OrderBy(node => node.Canonical, StringComparer.Ordinal))
        {
            Visit(node.Canonical);
        }

        void Visit(string canonical)
        {
            if (state.TryGetValue(canonical, out var currentState))
            {
                if (currentState == 1)
                {
                    var start = stack.IndexOf(canonical);
                    var cycle = stack.Skip(start).Append(canonical).ToArray();
                    var identity = string.Join(" -> ", cycle);
                    if (reported.Add(string.Join("|", cycle.OrderBy(value => value, StringComparer.Ordinal))))
                    {
                        diagnostics.Add(new GeneratorDiagnostic(
                            GeneratorDiagnosticCodes.InheritanceCycle,
                            GeneratorDiagnosticSeverity.Error,
                            $"Inheritance cycle detected: {identity}.",
                            "<definition-dependency-graph>",
                            canonical));
                    }
                }
                return;
            }

            state[canonical] = 1;
            stack.Add(canonical);
            if (bases.TryGetValue(canonical, out var baseCanonical))
            {
                Visit(baseCanonical);
            }
            stack.RemoveAt(stack.Count - 1);
            state[canonical] = 2;
        }
    }

    private static IEnumerable<DefinitionDependencyEdge> OrderEdges(
        IEnumerable<DefinitionDependencyEdge> edges) =>
        edges
            .OrderBy(edge => edge.SourceCanonical, StringComparer.Ordinal)
            .ThenBy(edge => edge.SourceElementId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Kind)
            .ThenBy(edge => edge.TargetCanonical, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetElementId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ReferenceIdentity, StringComparer.Ordinal);

    private static GeneratorDiagnostic CreateDiagnostic(
        string code,
        DefinitionDependencyNode source,
        string message) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            source.InventoryItem.SourceIdentity,
            source.Canonical,
            source.InventoryItem.DefinitionVersion);

    private static GeneratorDiagnostic CreateElementDiagnostic(
        string code,
        DefinitionDependencyNode source,
        ElementDefinitionDto element,
        string message) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            source.InventoryItem.SourceIdentity,
            source.Canonical,
            source.InventoryItem.DefinitionVersion,
            element.Id,
            element.Path);

    private static GenerationResult<DefinitionDependencyGraph?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(null, OrderDiagnostics(diagnostics));

    internal static GeneratorDiagnostic[] OrderDiagnostics(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.DefinitionCanonical, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.ElementId, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
}
