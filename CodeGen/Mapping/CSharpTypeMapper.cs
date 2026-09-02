using System.Diagnostics.CodeAnalysis;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class CSharpTypeMapper
{
    private readonly DefinitionTypeMappingView _definitionMappings;
    private readonly CSharpNameConverter _nameConverter;
    private readonly PrimitiveTypeMappingView _primitiveMappings;

    public CSharpTypeMapper(
        PrimitiveTypeMappingView primitiveMappings,
        DefinitionTypeMappingView definitionMappings,
        CSharpNameConverter? nameConverter = null)
    {
        ArgumentNullException.ThrowIfNull(primitiveMappings);
        ArgumentNullException.ThrowIfNull(definitionMappings);

        _primitiveMappings = primitiveMappings;
        _definitionMappings = definitionMappings;
        _nameConverter = nameConverter ?? new CSharpNameConverter();
    }

    public bool TryMap(
        string? fhirTypeCode,
        [NotNullWhen(true)] out CSharpTypeMapping? mapping)
    {
        return TryMapCore(
            fhirTypeCode,
            previewFhirTypeNames: null,
            previewNamespace: null,
            out mapping);
    }

    public bool TryMap(
        string? fhirTypeCode,
        IReadOnlySet<string> previewFhirTypeNames,
        string previewNamespace,
        [NotNullWhen(true)] out CSharpTypeMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(previewFhirTypeNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewNamespace);

        return TryMapCore(
            fhirTypeCode,
            previewFhirTypeNames,
            previewNamespace,
            out mapping);
    }

    private bool TryMapCore(
        string? fhirTypeCode,
        IReadOnlySet<string>? previewFhirTypeNames,
        string? previewNamespace,
        [NotNullWhen(true)] out CSharpTypeMapping? mapping)
    {
        mapping = null;

        if (string.IsNullOrWhiteSpace(fhirTypeCode))
        {
            return false;
        }

        if (_primitiveMappings.TryGet(fhirTypeCode, out var primitiveMapping))
        {
            mapping = CreateMapping(
                fhirTypeCode,
                primitiveMapping.WrapperName,
                primitiveMapping.Namespace,
                CSharpTypeCategory.Primitive,
                isPreviewType: false);
            return true;
        }

        var isPreviewType =
            previewFhirTypeNames?.Contains(fhirTypeCode) == true;
        DefinitionTypeMapping? definitionMapping = null;
        if (!isPreviewType && !_definitionMappings.TryGet(fhirTypeCode, out definitionMapping))
        {
            return false;
        }

        var nameResult = _nameConverter.ConvertTypeName(fhirTypeCode);
        if (!nameResult.IsSuccess)
        {
            return false;
        }

        var typeName = isPreviewType ? nameResult.Name! : definitionMapping!.TypeName;
        var targetNamespace = isPreviewType ? previewNamespace! : definitionMapping!.Namespace;
        mapping = CreateMapping(
            fhirTypeCode,
            typeName,
            targetNamespace,
            CSharpTypeCategory.Complex,
            isPreviewType);
        return true;
    }

    private static CSharpTypeMapping CreateMapping(
        string fhirTypeCode,
        string typeName,
        string targetNamespace,
        CSharpTypeCategory category,
        bool isPreviewType)
    {
        return new CSharpTypeMapping(
            fhirTypeCode,
            typeName,
            $"{targetNamespace}.{typeName}",
            category,
            targetNamespace,
            isPreviewType);
    }
}
