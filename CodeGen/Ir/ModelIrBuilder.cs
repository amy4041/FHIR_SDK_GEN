using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelIrBuilder
{
    private readonly CardinalityMapper _cardinalityMapper;
    private readonly CSharpNameConverter _nameConverter;

    public ModelIrBuilder(
        CSharpNameConverter? nameConverter = null,
        CardinalityMapper? cardinalityMapper = null)
    {
        _nameConverter = nameConverter ?? new CSharpNameConverter();
        _cardinalityMapper = cardinalityMapper ?? new CardinalityMapper();
    }

    public GenerationResult<ModelIrBatch?> Build(
        DefinitionDependencyGraph graph,
        GenerationScope scope,
        PrimitiveTypeMappingView primitiveMappings,
        ModelIrGenerationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(primitiveMappings);
        ArgumentNullException.ThrowIfNull(policy);

        var diagnostics = new List<GeneratorDiagnostic>();
        if (!string.Equals(
                graph.PackageIdentity.FhirVersion,
                policy.FhirVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                GeneratorDiagnosticSeverity.Error,
                $"Model IR policy FHIR version '{policy.FhirVersion}' does not match graph version '{graph.PackageIdentity.FhirVersion}'.",
                "<model-ir-policy>",
                DefinitionVersion: policy.FhirVersion));
            return Failure(diagnostics);
        }

        var definitionMappings = DefinitionTypeMappingView.FromGraph(graph);
        var drafts = CreateDeclarationDrafts(
            graph,
            scope,
            policy,
            definitionMappings,
            diagnostics);
        var backboneTargets = drafts
            .Where(draft => draft.Category == ModelIrCategory.Backbone)
            .ToDictionary(
                draft => (draft.Source.DefinitionCanonical, draft.BackboneElementId!),
                draft => draft,
                EqualityComparer<(string, string)>.Default);

        foreach (var node in scope.GeneratedModels.OrderBy(
            node => node.Canonical,
            StringComparer.Ordinal))
        {
            BuildMembers(
                node,
                graph,
                drafts,
                backboneTargets,
                primitiveMappings,
                definitionMappings,
                policy,
                diagnostics);
        }

        ValidateCollisions(drafts, graph, policy, diagnostics);
        if (diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        var declarations = drafts
            .OrderBy(draft => draft.Namespace, StringComparer.Ordinal)
            .ThenBy(draft => draft.CSharpName, StringComparer.Ordinal)
            .Select(draft => draft.ToIr())
            .ToArray();
        return new GenerationResult<ModelIrBatch?>(
            new ModelIrBatch(declarations),
            Array.Empty<GeneratorDiagnostic>());
    }

    private List<DeclarationDraft> CreateDeclarationDrafts(
        DefinitionDependencyGraph graph,
        GenerationScope scope,
        ModelIrGenerationPolicy policy,
        DefinitionTypeMappingView definitionMappings,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var drafts = new List<DeclarationDraft>();
        foreach (var node in scope.GeneratedModels)
        {
            var name = _nameConverter.ConvertTypeName(node.FhirTypeName);
            if (!name.IsSuccess)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    node,
                    $"FHIR type '{node.FhirTypeName}' cannot be converted to a C# type name."));
                continue;
            }

            var category = string.Equals(node.Kind, "resource", StringComparison.Ordinal)
                ? ModelIrCategory.Resource
                : ModelIrCategory.ComplexDatatype;
            var targetNamespace = category == ModelIrCategory.Resource
                ? policy.ResourceNamespace
                : policy.DatatypeNamespace;
            var baseType = ResolveBaseType(graph, node, definitionMappings, diagnostics);
            if (baseType is null)
            {
                continue;
            }

            var artifactPath = category == ModelIrCategory.Resource
                ? $"Generated/R5/Resources/{name.Name}/{name.Name}.g.cs"
                : $"Generated/R5/Types/{name.Name}.g.cs";
            drafts.Add(new DeclarationDraft(
                CreateSource(node),
                category,
                node.FhirTypeName,
                name.Name!,
                targetNamespace,
                artifactPath,
                node.InventoryItem.IsAbstract,
                isSealed: false,
                baseType,
                resourceOwnerCanonical: category == ModelIrCategory.Resource
                    ? node.Canonical
                    : null,
                backboneElementId: null));

            foreach (var edge in graph.GetOutgoingEdges(node.Canonical)
                .Where(edge => edge.Kind == DefinitionDependencyEdgeKind.BackboneOwner)
                .OrderBy(edge => edge.SourceElementId, StringComparer.Ordinal))
            {
                var elementId = edge.SourceElementId!;
                var backboneName = CreateBackboneName(elementId, policy, node, diagnostics);
                if (backboneName is null)
                {
                    continue;
                }

                drafts.Add(new DeclarationDraft(
                    new ModelIrSource(
                        node.InventoryItem.SourceIdentity,
                        node.Canonical,
                        node.InventoryItem.DefinitionVersion,
                        elementId,
                        FindElement(node, elementId)?.Path),
                    ModelIrCategory.Backbone,
                    elementId,
                    backboneName,
                    policy.BackboneNamespace,
                    $"Generated/R5/Resources/{name.Name}/{backboneName}.g.cs",
                    isAbstract: false,
                    isSealed: true,
                    CreateSyntheticTypeReference(
                        "BackboneElement",
                        "http://hl7.org/fhir/StructureDefinition/BackboneElement",
                        policy.BackboneBaseClrType,
                        isAbstract: true,
                        isExternal: true),
                    node.Canonical,
                    elementId));
            }
        }

        return drafts;
    }

    private void BuildMembers(
        DefinitionDependencyNode node,
        DefinitionDependencyGraph graph,
        IReadOnlyList<DeclarationDraft> drafts,
        IReadOnlyDictionary<(string Canonical, string ElementId), DeclarationDraft> backboneTargets,
        PrimitiveTypeMappingView primitiveMappings,
        DefinitionTypeMappingView definitionMappings,
        ModelIrGenerationPolicy policy,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var main = drafts.SingleOrDefault(draft =>
            draft.Source.DefinitionCanonical == node.Canonical &&
            draft.Category != ModelIrCategory.Backbone);
        if (main is null)
        {
            return;
        }

        var backbones = drafts
            .Where(draft =>
                draft.ResourceOwnerCanonical == node.Canonical &&
                draft.Category == ModelIrCategory.Backbone)
            .OrderByDescending(draft => draft.BackboneElementId!.Length)
            .ToArray();
        var elements = node.InventoryItem.Definition.Snapshot?.Elements ?? [];
        var order = 0;
        foreach (var element in elements.Where(element => IsDirectElement(node, element)))
        {
            var container = backbones.FirstOrDefault(backbone =>
                !string.Equals(backbone.BackboneElementId, element.Id, StringComparison.Ordinal) &&
                element.Id!.StartsWith(backbone.BackboneElementId + ".", StringComparison.Ordinal)) ?? main;
            var member = BuildMember(
                node,
                element,
                graph,
                backboneTargets,
                primitiveMappings,
                definitionMappings,
                policy,
                order++,
                diagnostics);
            if (member is not null)
            {
                container.Members.Add(member);
            }
        }
    }

    private ModelMemberIr? BuildMember(
        DefinitionDependencyNode node,
        ElementDefinitionDto element,
        DefinitionDependencyGraph graph,
        IReadOnlyDictionary<(string Canonical, string ElementId), DeclarationDraft> backboneTargets,
        PrimitiveTypeMappingView primitiveMappings,
        DefinitionTypeMappingView definitionMappings,
        ModelIrGenerationPolicy policy,
        int order,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(element.Id) || string.IsNullOrWhiteSpace(element.Path))
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                node,
                element,
                "A model IR element requires id and path."));
            return null;
        }

        if (!_cardinalityMapper.TryMap(element.Min, element.Max, out var cardinality))
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                node,
                element,
                $"Element cardinality '{element.Min?.ToString() ?? "<missing>"}..{element.Max ?? "<missing>"}' is unsupported."));
            return null;
        }

        var fhirName = LastSegment(element.Path);
        var isChoice = fhirName.EndsWith("[x]", StringComparison.Ordinal);
        var choiceStem = isChoice ? fhirName[..^3] : null;
        var isOpenType = isChoice && policy.OpenTypeElementIds.Contains(element.Id);
        var contentEdge = graph.GetOutgoingEdges(node.Canonical).SingleOrDefault(edge =>
            edge.Kind == DefinitionDependencyEdgeKind.ContentReference &&
            string.Equals(edge.SourceElementId, element.Id, StringComparison.Ordinal));
        var isBackbone = backboneTargets.ContainsKey((node.Canonical, element.Id));
        var representation = isOpenType
            ? ModelMemberRepresentation.OpenType
            : isChoice
                ? ModelMemberRepresentation.OrdinaryChoice
                : isBackbone
                    ? ModelMemberRepresentation.Backbone
                    : contentEdge is not null
                        ? ModelMemberRepresentation.ContentReference
                        : ModelMemberRepresentation.Standard;

        if (isChoice && cardinality.IsCollection)
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                node,
                element,
                "Choice elements with collection cardinality are not approved by the C0 policy."));
        }

        var alternatives = contentEdge is null
            ? ResolveAlternatives(
                node,
                element,
                graph,
                backboneTargets,
                primitiveMappings,
                definitionMappings,
                diagnostics)
            : ResolveContentReference(
                node,
                element,
                contentEdge,
                graph,
                backboneTargets,
                primitiveMappings,
                definitionMappings,
                diagnostics);
        if ((representation == ModelMemberRepresentation.Standard && alternatives.Count != 1) ||
            (representation == ModelMemberRepresentation.Backbone && alternatives.Count != 1) ||
            (representation == ModelMemberRepresentation.ContentReference && alternatives.Count != 1) ||
            (representation is ModelMemberRepresentation.OrdinaryChoice or ModelMemberRepresentation.OpenType &&
             alternatives.Count == 0))
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                node,
                element,
                $"Element representation '{representation}' has an invalid type alternative count '{alternatives.Count}'."));
        }

        var properties = CreateProperties(
            node,
            element,
            representation,
            fhirName,
            choiceStem,
            cardinality,
            alternatives,
            policy,
            diagnostics);
        var targetSource = contentEdge is null
            ? null
            : CreateResolvedElementSource(graph, contentEdge);
        var validation = CreateValidation(node, element, diagnostics);
        return new ModelMemberIr(
            new ModelIrSource(
                node.InventoryItem.SourceIdentity,
                node.Canonical,
                node.InventoryItem.DefinitionVersion,
                element.Id,
                element.Path),
            fhirName,
            fhirName,
            representation,
            cardinality,
            choiceStem,
            element.ContentReference,
            targetSource,
            alternatives,
            properties,
            validation,
            !string.IsNullOrWhiteSpace(element.Definition)
                ? element.Definition
                : element.Short,
            order);
    }

    private IReadOnlyList<ModelTypeReferenceIr> ResolveAlternatives(
        DefinitionDependencyNode node,
        ElementDefinitionDto element,
        DefinitionDependencyGraph graph,
        IReadOnlyDictionary<(string Canonical, string ElementId), DeclarationDraft> backboneTargets,
        PrimitiveTypeMappingView primitiveMappings,
        DefinitionTypeMappingView definitionMappings,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (backboneTargets.TryGetValue((node.Canonical, element.Id!), out var backbone))
        {
            return [CreateSyntheticTypeReference(
                "BackboneElement",
                node.Canonical,
                $"{backbone.Namespace}.{backbone.CSharpName}",
                isAbstract: false,
                isExternal: false,
                element.Id)];
        }

        var result = new List<ModelTypeReferenceIr>();
        foreach (var type in element.Types ?? [])
        {
            if (string.IsNullOrWhiteSpace(type.Code))
            {
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    node,
                    element,
                    "A type alternative requires type.code."));
                continue;
            }

            var edge = graph.GetOutgoingEdges(node.Canonical).FirstOrDefault(candidate =>
                candidate.Kind == DefinitionDependencyEdgeKind.ElementType &&
                string.Equals(candidate.SourceElementId, element.Id, StringComparison.Ordinal) &&
                string.Equals(candidate.ReferenceIdentity, type.Code, StringComparison.Ordinal));
            if (edge is null || !graph.TryGetNode(edge.TargetCanonical, out var target) || target is null)
            {
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.MissingDependency,
                    node,
                    element,
                    $"Type alternative '{type.Code}' has no resolved graph edge."));
                continue;
            }

            string? clrType = null;
            var supported = true;
            var isPrimitive = target.Disposition is
                DefinitionDependencyNodeDisposition.SupportedPrimitive or
                DefinitionDependencyNodeDisposition.UnsupportedPrimitive;
            if (isPrimitive)
            {
                if (primitiveMappings.TryGet(type.Code, out var primitive))
                {
                    clrType = $"{primitive.Namespace}.{primitive.WrapperName}";
                }
                else
                {
                    supported = false;
                    diagnostics.Add(CreateElementDiagnostic(
                        GeneratorDiagnosticCodes.UnsupportedPrimitiveReference,
                        node,
                        element,
                        $"Type alternative '{type.Code}' is an unsupported primitive."));
                }
            }
            else if (definitionMappings.TryGet(type.Code, out var definitionMapping))
            {
                clrType = $"{definitionMapping.Namespace}.{definitionMapping.TypeName}";
            }
            else
            {
                supported = false;
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.MissingTypeMapping,
                    node,
                    element,
                    $"Type alternative '{type.Code}' has no CLR mapping."));
            }

            result.Add(new ModelTypeReferenceIr(
                type.Code,
                target.Canonical,
                null,
                clrType,
                target.InventoryItem.IsAbstract,
                target.Disposition == DefinitionDependencyNodeDisposition.ExternalHandwritten,
                isPrimitive,
                supported,
                (type.Profiles ?? []).Order(StringComparer.Ordinal),
                (type.TargetProfiles ?? []).Order(StringComparer.Ordinal)));
        }

        return result;
    }

    private IReadOnlyList<ModelTypeReferenceIr> ResolveContentReference(
        DefinitionDependencyNode node,
        ElementDefinitionDto element,
        DefinitionDependencyEdge contentEdge,
        DefinitionDependencyGraph graph,
        IReadOnlyDictionary<(string Canonical, string ElementId), DeclarationDraft> backboneTargets,
        PrimitiveTypeMappingView primitiveMappings,
        DefinitionTypeMappingView definitionMappings,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (backboneTargets.TryGetValue(
                (contentEdge.TargetCanonical, contentEdge.TargetElementId!),
                out var backbone))
        {
            return [CreateSyntheticTypeReference(
                "BackboneElement",
                contentEdge.TargetCanonical,
                $"{backbone.Namespace}.{backbone.CSharpName}",
                false,
                false,
                contentEdge.TargetElementId)];
        }

        if (!graph.TryGetNode(contentEdge.TargetCanonical, out var targetNode) || targetNode is null ||
            FindElement(targetNode, contentEdge.TargetElementId!) is not { } targetElement)
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.MissingDependency,
                node,
                element,
                $"Resolved contentReference target '{contentEdge.TargetCanonical}#{contentEdge.TargetElementId}' is unavailable."));
            return [];
        }

        return ResolveAlternatives(
            targetNode,
            targetElement,
            graph,
            backboneTargets,
            primitiveMappings,
            definitionMappings,
            diagnostics);
    }

    private IReadOnlyList<ModelPropertyIr> CreateProperties(
        DefinitionDependencyNode node,
        ElementDefinitionDto element,
        ModelMemberRepresentation representation,
        string fhirName,
        string? choiceStem,
        CardinalityModel cardinality,
        IReadOnlyList<ModelTypeReferenceIr> alternatives,
        ModelIrGenerationPolicy policy,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (representation == ModelMemberRepresentation.OrdinaryChoice)
        {
            var stemResult = _nameConverter.ConvertPropertyName(choiceStem);
            if (!stemResult.IsSuccess)
            {
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    node,
                    element,
                    $"Choice stem '{choiceStem}' cannot be converted to a C# property name."));
                return [];
            }

            return alternatives.Select(alternative =>
            {
                var suffix = _nameConverter.ConvertTypeName(alternative.FhirTypeCode);
                if (!suffix.IsSuccess)
                {
                    diagnostics.Add(CreateElementDiagnostic(
                        GeneratorDiagnosticCodes.InvalidModelIr,
                        node,
                        element,
                        $"Choice type suffix '{alternative.FhirTypeCode}' cannot be converted to C#."));
                }
                var suffixName = suffix.Name ?? alternative.FhirTypeCode;
                return new ModelPropertyIr(
                    choiceStem + suffixName,
                    choiceStem + suffixName,
                    stemResult.Name + suffixName,
                    alternative.ClrType,
                    IsNullable: true,
                    IsCollection: false,
                    alternative);
            }).ToArray();
        }

        var sourceName = representation == ModelMemberRepresentation.OpenType
            ? choiceStem!
            : fhirName;
        var explicitRename = policy.MemberRenames.GetValueOrDefault(element.Id!);
        var nameResult = _nameConverter.ConvertPropertyName(sourceName);
        if (explicitRename is null && !nameResult.IsSuccess)
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                node,
                element,
                $"FHIR member '{sourceName}' cannot be converted to a C# property name."));
            return [];
        }

        var propertyType = representation == ModelMemberRepresentation.OpenType
            ? policy.OpenTypeClrType
            : alternatives.FirstOrDefault()?.ClrType;
        return [new ModelPropertyIr(
            sourceName,
            explicitRename?.JsonName ?? sourceName,
            explicitRename?.ClrName ?? nameResult.Name!,
            propertyType,
            IsNullable: !cardinality.IsRequired || representation is
                ModelMemberRepresentation.OrdinaryChoice or ModelMemberRepresentation.OpenType,
            cardinality.IsCollection,
            representation == ModelMemberRepresentation.OpenType
                ? null
                : alternatives.FirstOrDefault())];
    }

    private static ModelValidationMetadataIr CreateValidation(
        DefinitionDependencyNode node,
        ElementDefinitionDto element,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var constraints = new List<ModelConstraintIr>();
        foreach (var constraint in element.Constraints ?? [])
        {
            if (string.IsNullOrWhiteSpace(constraint.Key) ||
                string.IsNullOrWhiteSpace(constraint.Severity) ||
                string.IsNullOrWhiteSpace(constraint.Human))
            {
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    node,
                    element,
                    "Constraint metadata requires key, severity, and human text."));
                continue;
            }
            constraints.Add(new ModelConstraintIr(
                constraint.Key,
                constraint.Severity,
                constraint.Human,
                constraint.Expression,
                constraint.Source));
        }

        ModelBindingIr? binding = null;
        if (element.Binding is not null)
        {
            if (string.IsNullOrWhiteSpace(element.Binding.Strength))
            {
                diagnostics.Add(CreateElementDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    node,
                    element,
                    "Binding metadata requires strength."));
            }
            else
            {
                binding = new ModelBindingIr(
                    element.Binding.Strength,
                    element.Binding.Description,
                    element.Binding.ValueSet);
            }
        }

        var fixedValues = ReadRawValues(element, "fixed");
        var patternValues = ReadRawValues(element, "pattern");
        if (fixedValues.Count > 0 || patternValues.Count > 0)
        {
            diagnostics.Add(CreateElementDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                node,
                element,
                "Fixed or pattern values are not approved in the generated specialization scope."));
        }

        return new ModelValidationMetadataIr(
            constraints.OrderBy(item => item.Key, StringComparer.Ordinal),
            binding,
            element.MustSupport,
            element.IsModifier,
            element.IsModifierReason,
            element.IsSummary,
            (element.Conditions ?? []).Order(StringComparer.Ordinal),
            element.Slicing?.GetRawText(),
            fixedValues,
            patternValues,
            element.Label,
            (element.Aliases ?? []).Order(StringComparer.Ordinal),
            (element.Representations ?? []).Order(StringComparer.Ordinal),
            element.Comment,
            element.Requirements,
            element.MeaningWhenMissing,
            element.OrderMeaning);
    }

    private static IReadOnlyList<ModelRawValueIr> ReadRawValues(
        ElementDefinitionDto element,
        string prefix) =>
        (element.AdditionalProperties ?? [])
            .Where(property => property.Key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => new ModelRawValueIr(property.Key, property.Value.GetRawText()))
            .ToArray();

    private string? CreateBackboneName(
        string elementId,
        ModelIrGenerationPolicy policy,
        DefinitionDependencyNode node,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (policy.BackboneRenames.TryGetValue(elementId, out var rename))
        {
            return rename.ClrName;
        }

        var segments = elementId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var converted = new List<string>();
        foreach (var segment in segments)
        {
            var result = _nameConverter.ConvertTypeName(segment);
            if (!result.IsSuccess)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    node,
                    $"Backbone identity '{elementId}' contains invalid segment '{segment}'.",
                    elementId));
                return null;
            }
            converted.Add(result.Name!);
        }
        return string.Concat(converted);
    }

    private static ModelTypeReferenceIr? ResolveBaseType(
        DefinitionDependencyGraph graph,
        DefinitionDependencyNode node,
        DefinitionTypeMappingView definitionMappings,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var edge = graph.GetOutgoingEdges(node.Canonical).SingleOrDefault(candidate =>
            candidate.Kind == DefinitionDependencyEdgeKind.Inheritance);
        if (edge is null || !graph.TryGetNode(edge.TargetCanonical, out var target) || target is null ||
            !definitionMappings.TryGet(target.FhirTypeName, out var mapping))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.MissingDependency,
                node,
                "A generated model requires one resolved inheritance edge and CLR base mapping."));
            return null;
        }

        return new ModelTypeReferenceIr(
            target.FhirTypeName,
            target.Canonical,
            null,
            $"{mapping.Namespace}.{mapping.TypeName}",
            target.InventoryItem.IsAbstract,
            target.Disposition == DefinitionDependencyNodeDisposition.ExternalHandwritten,
            false,
            true,
            [],
            []);
    }

    private static ModelTypeReferenceIr CreateSyntheticTypeReference(
        string fhirTypeCode,
        string targetCanonical,
        string clrType,
        bool isAbstract,
        bool isExternal,
        string? targetElementId = null) =>
        new(
            fhirTypeCode,
            targetCanonical,
            targetElementId,
            clrType,
            isAbstract,
            isExternal,
            false,
            true,
            [],
            []);

    private static ModelIrSource? CreateResolvedElementSource(
        DefinitionDependencyGraph graph,
        DefinitionDependencyEdge edge)
    {
        if (!graph.TryGetNode(edge.TargetCanonical, out var target) || target is null)
        {
            return null;
        }
        var element = FindElement(target, edge.TargetElementId!);
        return new ModelIrSource(
            target.InventoryItem.SourceIdentity,
            target.Canonical,
            target.InventoryItem.DefinitionVersion,
            edge.TargetElementId,
            element?.Path);
    }

    private static void ValidateCollisions(
        IReadOnlyList<DeclarationDraft> drafts,
        DefinitionDependencyGraph graph,
        ModelIrGenerationPolicy policy,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        AddDuplicateDiagnostics(
            drafts,
            draft => $"{draft.Namespace}.{draft.CSharpName}",
            "fully qualified type",
            StringComparer.Ordinal,
            diagnostics);
        AddDuplicateDiagnostics(
            drafts,
            draft => draft.ArtifactPath,
            "artifact path",
            StringComparer.OrdinalIgnoreCase,
            diagnostics);

        var byCanonical = drafts
            .Where(draft => draft.Category != ModelIrCategory.Backbone)
            .ToDictionary(draft => draft.Source.DefinitionCanonical, StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var jsonNames = new HashSet<string>(StringComparer.Ordinal);
            if (draft.Category == ModelIrCategory.Resource && !draft.IsAbstract)
            {
                names.UnionWith(policy.SyntheticResourceMemberNames);
            }
            foreach (var inherited in GetInheritedNames(draft, graph, byCanonical))
            {
                names.Add(inherited.CSharpName);
                jsonNames.Add(inherited.JsonName);
            }
            foreach (var property in draft.Members.SelectMany(member => member.Properties))
            {
                if (string.Equals(property.CSharpName, draft.CSharpName, StringComparison.Ordinal) ||
                    !names.Add(property.CSharpName))
                {
                    diagnostics.Add(new GeneratorDiagnostic(
                        GeneratorDiagnosticCodes.ModelIrCollision,
                        GeneratorDiagnosticSeverity.Error,
                        $"C# member '{property.CSharpName}' collides in declaration '{draft.Namespace}.{draft.CSharpName}'.",
                        draft.Source.SourceIdentity,
                        draft.Source.DefinitionCanonical,
                        draft.Source.DefinitionVersion,
                        draft.Members.First(member => member.Properties.Contains(property)).Source.ElementId,
                        draft.Members.First(member => member.Properties.Contains(property)).Source.ElementPath));
                }
                if (!jsonNames.Add(property.JsonName))
                {
                    diagnostics.Add(new GeneratorDiagnostic(
                        GeneratorDiagnosticCodes.ModelIrCollision,
                        GeneratorDiagnosticSeverity.Error,
                        $"JSON member '{property.JsonName}' collides in declaration '{draft.Namespace}.{draft.CSharpName}'.",
                        draft.Source.SourceIdentity,
                        draft.Source.DefinitionCanonical,
                        draft.Source.DefinitionVersion,
                        draft.Members.First(member => member.Properties.Contains(property)).Source.ElementId,
                        draft.Members.First(member => member.Properties.Contains(property)).Source.ElementPath));
                }
            }
        }
    }

    private static IEnumerable<(string CSharpName, string JsonName)> GetInheritedNames(
        DeclarationDraft draft,
        DefinitionDependencyGraph graph,
        IReadOnlyDictionary<string, DeclarationDraft> byCanonical)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var canonical = draft.BaseType.TargetCanonical;
        while (visited.Add(canonical) && byCanonical.TryGetValue(canonical, out var baseDraft))
        {
            foreach (var property in baseDraft.Members.SelectMany(member => member.Properties))
            {
                yield return (property.CSharpName, property.JsonName);
            }
            var edge = graph.GetOutgoingEdges(canonical).SingleOrDefault(candidate =>
                candidate.Kind == DefinitionDependencyEdgeKind.Inheritance);
            if (edge is null)
            {
                yield break;
            }
            canonical = edge.TargetCanonical;
        }
    }

    private static void AddDuplicateDiagnostics(
        IEnumerable<DeclarationDraft> drafts,
        Func<DeclarationDraft, string> keySelector,
        string identityKind,
        StringComparer comparer,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var group in drafts.GroupBy(keySelector, comparer)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            foreach (var duplicate in group.OrderBy(draft => draft.Source.ElementId, StringComparer.Ordinal).Skip(1))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.ModelIrCollision,
                    GeneratorDiagnosticSeverity.Error,
                    $"Model IR {identityKind} '{group.Key}' is duplicated.",
                    duplicate.Source.SourceIdentity,
                    duplicate.Source.DefinitionCanonical,
                    duplicate.Source.DefinitionVersion,
                    duplicate.Source.ElementId,
                    duplicate.Source.ElementPath));
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

    private static ElementDefinitionDto? FindElement(
        DefinitionDependencyNode node,
        string elementId) =>
        node.InventoryItem.Definition.Snapshot?.Elements?.FirstOrDefault(element =>
            string.Equals(element.Id, elementId, StringComparison.Ordinal));

    private static string LastSegment(string path)
    {
        var index = path.LastIndexOf('.');
        return index < 0 ? path : path[(index + 1)..];
    }

    private static ModelIrSource CreateSource(DefinitionDependencyNode node) =>
        new(
            node.InventoryItem.SourceIdentity,
            node.Canonical,
            node.InventoryItem.DefinitionVersion);

    private static GeneratorDiagnostic CreateDiagnostic(
        string code,
        DefinitionDependencyNode node,
        string message,
        string? elementId = null) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            node.InventoryItem.SourceIdentity,
            node.Canonical,
            node.InventoryItem.DefinitionVersion,
            elementId);

    private static GeneratorDiagnostic CreateElementDiagnostic(
        string code,
        DefinitionDependencyNode node,
        ElementDefinitionDto element,
        string message) =>
        new(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            node.InventoryItem.SourceIdentity,
            node.Canonical,
            node.InventoryItem.DefinitionVersion,
            element.Id,
            element.Path);

    private static GenerationResult<ModelIrBatch?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(
            null,
            diagnostics
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.DefinitionCanonical, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.ElementId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToArray());

    private sealed class DeclarationDraft
    {
        internal DeclarationDraft(
            ModelIrSource source,
            ModelIrCategory category,
            string fhirName,
            string cSharpName,
            string @namespace,
            string artifactPath,
            bool isAbstract,
            bool isSealed,
            ModelTypeReferenceIr baseType,
            string? resourceOwnerCanonical,
            string? backboneElementId)
        {
            Source = source;
            Category = category;
            FhirName = fhirName;
            CSharpName = cSharpName;
            Namespace = @namespace;
            ArtifactPath = artifactPath;
            IsAbstract = isAbstract;
            IsSealed = isSealed;
            BaseType = baseType;
            ResourceOwnerCanonical = resourceOwnerCanonical;
            BackboneElementId = backboneElementId;
        }

        internal ModelIrSource Source { get; }
        internal ModelIrCategory Category { get; }
        internal string FhirName { get; }
        internal string CSharpName { get; }
        internal string Namespace { get; }
        internal string ArtifactPath { get; }
        internal bool IsAbstract { get; }
        internal bool IsSealed { get; }
        internal ModelTypeReferenceIr BaseType { get; }
        internal string? ResourceOwnerCanonical { get; }
        internal string? BackboneElementId { get; }
        internal List<ModelMemberIr> Members { get; } = [];

        internal ModelDeclarationIr ToIr() =>
            new(
                Source,
                Category,
                FhirName,
                CSharpName,
                Namespace,
                ArtifactPath,
                IsAbstract,
                IsSealed,
                BaseType,
                ResourceOwnerCanonical,
                BackboneElementId,
                Members.OrderBy(member => member.Order));
    }
}
