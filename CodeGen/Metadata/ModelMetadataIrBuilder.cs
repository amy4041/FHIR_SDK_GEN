using System.Collections;
using MyFhirSdk.Core;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Mapping;

namespace MyFhirSdk.CodeGen.Metadata;

public sealed class ModelMetadataIrBuilder
{
    private readonly CSharpNameConverter _nameConverter = new();

    public GenerationResult<ModelMetadataIrBatch?> Build(ModelIrBatch modelIr)
    {
        ArgumentNullException.ThrowIfNull(modelIr);
        var declarations = modelIr.Declarations
            .OrderBy(declaration => declaration.FullyQualifiedName, StringComparer.Ordinal)
            .ToArray();
        var diagnostics = new List<GeneratorDiagnostic>();

        var resources = declarations
            .Where(declaration =>
                declaration.Category == ModelIrCategory.Resource &&
                !declaration.IsAbstract)
            .Select(declaration => new ResourceFactoryMetadataIr(
                declaration.FhirName,
                declaration.FullyQualifiedName))
            .ToArray();
        AddDuplicateDiagnostics(
            resources,
            resource => resource.FhirTypeName,
            "FHIR Resource type name",
            diagnostics);
        AddDuplicateDiagnostics(
            resources,
            resource => resource.ClrType,
            "Resource CLR type",
            diagnostics);

        var generatedDatatypes = declarations
            .Where(declaration =>
                declaration.Category == ModelIrCategory.ComplexDatatype &&
                !declaration.IsAbstract)
            .Select(declaration => new ConcreteDatatypeMetadataIr(declaration.FullyQualifiedName))
            .ToArray();
        var externalRuntimeTypes = ResolveExternalRuntimeTypes(modelIr.ExternalMetadata, diagnostics);
        var concreteDatatypes = generatedDatatypes
            .Concat(externalRuntimeTypes
                .Where(item =>
                    !item.Metadata.IsAbstract &&
                    typeof(DataType).IsAssignableFrom(item.RuntimeType) &&
                    !item.RuntimeType.IsAbstract &&
                    !item.RuntimeType.IsInterface)
                .Select(item => new ConcreteDatatypeMetadataIr(item.Metadata.ClrType)))
            .OrderBy(item => item.ClrType, StringComparer.Ordinal)
            .ToArray();
        AddDuplicateDiagnostics(
            concreteDatatypes,
            item => item.ClrType,
            "concrete datatype CLR type",
            diagnostics);

        var declaredDatatypes = CreateDeclaredDatatypes(externalRuntimeTypes, diagnostics);

        var openMembers = declarations
            .SelectMany(declaration => declaration.Members
                .Where(member => member.Representation == ModelMemberRepresentation.OpenType)
                .Select(member => (Declaration: declaration, Member: member)))
            .ToArray();
        var openTypes = openMembers
            .SelectMany(item => item.Member.TypeAlternatives.Select(alternative =>
            {
                var suffix = _nameConverter.ConvertTypeName(alternative.FhirTypeCode);
                if (!suffix.IsSuccess)
                {
                    diagnostics.Add(CreateDiagnostic(
                        item.Declaration,
                        item.Member,
                        $"Open-type alternative '{alternative.FhirTypeCode}' has no valid JSON suffix."));
                }
                return new OpenTypeMetadataIr(
                    item.Declaration.FullyQualifiedName,
                    item.Member.Properties.Single().CSharpName,
                    item.Member.Source.ElementId!,
                    alternative.FhirTypeCode,
                    alternative.ClrType!,
                    item.Member.ChoiceStem + (suffix.Name ?? alternative.FhirTypeCode));
            }))
            .OrderBy(item => item.DeclaringClrType, StringComparer.Ordinal)
            .ThenBy(item => item.ChoiceElementId, StringComparer.Ordinal)
            .ThenBy(item => item.JsonPropertyName, StringComparer.Ordinal)
            .ToArray();
        AddDuplicateDiagnostics(
            openTypes,
            item => $"{item.DeclaringClrType}|{item.ClrPropertyName}|{item.JsonPropertyName}",
            "open-type parser identity",
            diagnostics);
        AddDuplicateDiagnostics(
            openTypes,
            item => $"{item.DeclaringClrType}|{item.ClrPropertyName}|{item.ValueClrType}",
            "open-type serializer identity",
            diagnostics);

        var extensionMembers = modelIr.ExternalMetadata
            .SelectMany(metadata => metadata.Members.Select(member =>
                (Metadata: metadata, Member: member)))
            .Where(item =>
                string.Equals(item.Member.Source.ElementId, "Extension.value[x]", StringComparison.Ordinal))
            .ToArray();
        if (extensionMembers.Length != 1)
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                GeneratorDiagnosticSeverity.Error,
                $"External Extension.value[x] metadata must occur once; found {extensionMembers.Length}.",
                "<model-metadata>"));
        }
        var extensionAlternatives = extensionMembers.Length == 0
            ? []
            : SelectAvailableExtensionAlternatives(
                extensionMembers[0].Member.TypeAlternatives,
                declarations);
        var extensionValues = extensionAlternatives
            .Select(alternative =>
            {
                var suffix = _nameConverter.ConvertTypeName(alternative.FhirTypeCode);
                if (!suffix.IsSuccess)
                {
                    diagnostics.Add(new GeneratorDiagnostic(
                        GeneratorDiagnosticCodes.InvalidModelIr,
                        GeneratorDiagnosticSeverity.Error,
                        $"Extension value alternative '{alternative.FhirTypeCode}' has no valid JSON suffix.",
                        "<model-metadata>"));
                }
                return new ExtensionValueMetadataIr(
                    alternative.FhirTypeCode,
                    alternative.ClrType!,
                    "value" + (suffix.Name ?? alternative.FhirTypeCode));
            })
            .OrderBy(item => item.JsonPropertyName, StringComparer.Ordinal)
            .ToArray();
        AddDuplicateDiagnostics(
            extensionValues,
            item => item.JsonPropertyName,
            "Extension value JSON property",
            diagnostics);
        AddConflictingExtensionDiagnostics(extensionValues, diagnostics);

        var validationTypes = declarations
            .Select(declaration => new ValidationTypeMetadataIr(
                declaration.FullyQualifiedName,
                CreateValidationRules(declaration)))
            .Concat(modelIr.ExternalMetadata.Select(metadata => new ValidationTypeMetadataIr(
                metadata.ClrType,
                CreateValidationRules(metadata))))
            .Where(item => item.Rules.Count > 0)
            .OrderBy(item => item.ClrType, StringComparer.Ordinal)
            .ToArray();

        if (diagnostics.Count > 0)
        {
            return new GenerationResult<ModelMetadataIrBatch?>(
                null,
                diagnostics.OrderBy(diagnostic => diagnostic.Message, StringComparer.Ordinal).ToArray());
        }

        return new GenerationResult<ModelMetadataIrBatch?>(
            new ModelMetadataIrBatch(
                resources,
                concreteDatatypes,
                declaredDatatypes,
                extensionValues,
                openTypes,
                validationTypes),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static IReadOnlyList<ModelTypeReferenceIr> SelectAvailableExtensionAlternatives(
        IEnumerable<ModelTypeReferenceIr> alternatives,
        IEnumerable<ModelDeclarationIr> declarations)
    {
        var generatedClrTypes = declarations
            .Select(declaration => declaration.FullyQualifiedName)
            .ToHashSet(StringComparer.Ordinal);
        return alternatives
            .Where(alternative =>
                alternative.IsSupported &&
                !string.IsNullOrWhiteSpace(alternative.ClrType) &&
                (alternative.IsPrimitive ||
                    alternative.IsExternal ||
                    generatedClrTypes.Contains(alternative.ClrType)))
            .ToArray();
    }

    private static IReadOnlyList<ValidationRuleMetadataIr> CreateValidationRules(
        ExternalModelMetadataIr metadata)
    {
        var rules = new List<ValidationRuleMetadataIr>();
        foreach (var member in metadata.Members)
        {
            if (member.Representation == ModelMemberRepresentation.OrdinaryChoice)
            {
                continue;
            }
            if (!member.Cardinality.IsRequired)
            {
                continue;
            }
            rules.Add(new ValidationRuleMetadataIr(
                member.Cardinality.IsCollection
                    ? ValidationRuleKind.RequiredCollection
                    : ValidationRuleKind.RequiredScalar,
                member.Source.ElementId!,
                member.FhirName,
                [member.ClrPropertyName]));
        }
        return rules;
    }

    private static IReadOnlyList<(ExternalModelMetadataIr Metadata, Type RuntimeType)>
        ResolveExternalRuntimeTypes(
            IEnumerable<ExternalModelMetadataIr> metadata,
            ICollection<GeneratorDiagnostic> diagnostics)
    {
        var result = new List<(ExternalModelMetadataIr, Type)>();
        var runtimeAssembly = typeof(FhirObject).Assembly;
        foreach (var item in metadata.OrderBy(item => item.ClrType, StringComparer.Ordinal))
        {
            var runtimeType = runtimeAssembly.GetType(item.ClrType, throwOnError: false);
            if (runtimeType is null)
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    GeneratorDiagnosticSeverity.Error,
                    $"External bootstrap CLR type '{item.ClrType}' is unavailable.",
                    item.Source.SourceIdentity,
                    item.Source.DefinitionCanonical,
                    item.Source.DefinitionVersion));
                continue;
            }
            result.Add((item, runtimeType));
        }
        return result;
    }

    private static IReadOnlyList<DeclaredDatatypeMetadataIr> CreateDeclaredDatatypes(
        IEnumerable<(ExternalModelMetadataIr Metadata, Type RuntimeType)> externalTypes,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var result = new List<DeclaredDatatypeMetadataIr>();
        foreach (var (metadata, runtimeType) in externalTypes)
        {
            foreach (var member in metadata.Members.Where(member =>
                         member.Representation == ModelMemberRepresentation.Standard &&
                         member.TypeAlternatives.Count == 1 &&
                         !string.IsNullOrWhiteSpace(member.TypeAlternatives[0].ClrType)))
            {
                var property = runtimeType.GetProperty(member.ClrPropertyName);
                if (property is null)
                {
                    continue;
                }
                var declaredType = GetPropertyElementType(property.PropertyType);
                if (declaredType != typeof(DataType))
                {
                    continue;
                }
                result.Add(new DeclaredDatatypeMetadataIr(
                    metadata.ClrType,
                    member.JsonName,
                    member.TypeAlternatives[0].ClrType!));
            }
        }
        AddDuplicateDiagnostics(
            result,
            item => $"{item.DeclaringClrType}|{item.PropertyName}",
            "declared datatype property",
            diagnostics);
        return result
            .OrderBy(item => item.DeclaringClrType, StringComparer.Ordinal)
            .ThenBy(item => item.PropertyName, StringComparer.Ordinal)
            .ToArray();
    }

    private static Type GetPropertyElementType(Type propertyType)
    {
        if (propertyType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(propertyType))
        {
            return propertyType;
        }
        return propertyType
            .GetInterfaces()
            .Append(propertyType)
            .Where(type => type.IsGenericType)
            .Where(type => type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(type => type.GetGenericArguments()[0])
            .FirstOrDefault() ?? propertyType;
    }

    private static void AddConflictingExtensionDiagnostics(
        IEnumerable<ExtensionValueMetadataIr> values,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var group in values.GroupBy(item => item.ClrType, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                var identityKind = group
                    .Select(item => (item.FhirTypeCode, item.JsonPropertyName))
                    .Distinct()
                    .Count() > 1
                        ? "conflicting FHIR JSON identities"
                        : "duplicate metadata";
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidModelIr,
                    GeneratorDiagnosticSeverity.Error,
                    $"Extension value CLR type '{group.Key}' has {identityKind}.",
                    "<model-metadata>"));
            }
        }
    }

    private static IReadOnlyList<ValidationRuleMetadataIr> CreateValidationRules(
        ModelDeclarationIr declaration)
    {
        var rules = new List<ValidationRuleMetadataIr>();
        foreach (var member in declaration.Members.OrderBy(member => member.Order))
        {
            if (member.Representation == ModelMemberRepresentation.OrdinaryChoice)
            {
                rules.Add(new ValidationRuleMetadataIr(
                    member.Cardinality.IsRequired
                        ? ValidationRuleKind.ChoiceExactlyOne
                        : ValidationRuleKind.ChoiceAtMostOne,
                    member.Source.ElementId!,
                    member.FhirName,
                    member.Properties.Select(property => property.CSharpName).ToArray()));
                continue;
            }

            if (!member.Cardinality.IsRequired)
            {
                continue;
            }

            var property = member.Properties.Single();
            rules.Add(new ValidationRuleMetadataIr(
                member.Cardinality.IsCollection
                    ? ValidationRuleKind.RequiredCollection
                    : ValidationRuleKind.RequiredScalar,
                member.Source.ElementId!,
                member.FhirName,
                [property.CSharpName]));
        }
        return rules;
    }

    private static void AddDuplicateDiagnostics<T>(
        IEnumerable<T> items,
        Func<T, string> getIdentity,
        string identityKind,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var duplicate in items
                     .GroupBy(getIdentity, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                GeneratorDiagnosticSeverity.Error,
                $"Duplicate {identityKind} '{duplicate.Key}'.",
                "<model-metadata>"));
        }
    }

    private static GeneratorDiagnostic CreateDiagnostic(
        ModelDeclarationIr declaration,
        ModelMemberIr member,
        string message) =>
        new(
            GeneratorDiagnosticCodes.InvalidModelIr,
            GeneratorDiagnosticSeverity.Error,
            message,
            declaration.Source.SourceIdentity,
            declaration.Source.DefinitionCanonical,
            declaration.Source.DefinitionVersion,
            member.Source.ElementId,
            member.Source.ElementPath);
}
