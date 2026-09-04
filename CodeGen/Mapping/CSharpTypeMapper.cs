using System.Diagnostics.CodeAnalysis;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class CSharpTypeMapper
{
    private readonly DefinitionTypeMappingView _definitionMappings;
    private readonly PrimitiveTypeMappingView _primitiveMappings;

    public CSharpTypeMapper(
        PrimitiveTypeMappingView primitiveMappings,
        DefinitionTypeMappingView definitionMappings)
    {
        ArgumentNullException.ThrowIfNull(primitiveMappings);
        ArgumentNullException.ThrowIfNull(definitionMappings);

        _primitiveMappings = primitiveMappings;
        _definitionMappings = definitionMappings;
    }

    public bool TryMap(
        string? fhirTypeCode,
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
                CSharpTypeCategory.Primitive);
            return true;
        }

        if (!_definitionMappings.TryGet(fhirTypeCode, out var definitionMapping))
        {
            return false;
        }

        mapping = CreateMapping(
            fhirTypeCode,
            definitionMapping.TypeName,
            definitionMapping.Namespace,
            CSharpTypeCategory.Complex);
        return true;
    }

    private static CSharpTypeMapping CreateMapping(
        string fhirTypeCode,
        string typeName,
        string targetNamespace,
        CSharpTypeCategory category)
    {
        return new CSharpTypeMapping(
            fhirTypeCode,
            typeName,
            $"{targetNamespace}.{typeName}",
            category,
            targetNamespace);
    }
}
