using System.Diagnostics.CodeAnalysis;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class CSharpTypeMapper
{
    private const string PrimitiveNamespace = "MyFhirSdk.Primitives";
    private const string ComplexTypeNamespace = "MyFhirSdk.Types";

    private static readonly IReadOnlyDictionary<string, string> PrimitiveTypeNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["boolean"] = "FhirBoolean",
            ["string"] = "FhirString",
            ["code"] = "FhirCode",
            ["id"] = "FhirId",
            ["uri"] = "FhirUri",
            ["url"] = "FhirUrl",
            ["canonical"] = "FhirCanonical",
            ["integer"] = "FhirInteger",
            ["decimal"] = "FhirDecimal",
            ["date"] = "FhirDate",
            ["dateTime"] = "FhirDateTime",
            ["instant"] = "FhirInstant",
            ["positiveInt"] = "FhirPositiveInt",
            ["unsignedInt"] = "FhirUnsignedInt",
            ["base64Binary"] = "FhirBase64Binary",
            ["markdown"] = "FhirMarkdown"
        };

    private static readonly IReadOnlySet<string> DefaultComplexTypeNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Address",
            "Attachment",
            "CodeableConcept",
            "CodeableReference",
            "Coding",
            "ContactPoint",
            "Duration",
            "ExtendedContactDetail",
            "HumanName",
            "Identifier",
            "Money",
            "Period",
            "Quantity",
            "Reference",
            "Signature",
            "SimpleQuantity",
            "VirtualServiceDetail"
        };

    private readonly IReadOnlySet<string> _knownComplexTypeNames;
    private readonly CSharpNameConverter _nameConverter;

    public CSharpTypeMapper(
        IEnumerable<string>? knownComplexTypeNames = null,
        CSharpNameConverter? nameConverter = null)
    {
        _knownComplexTypeNames = new HashSet<string>(
            knownComplexTypeNames ?? DefaultComplexTypeNames,
            StringComparer.Ordinal);
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

        if (PrimitiveTypeNames.TryGetValue(fhirTypeCode, out var primitiveTypeName))
        {
            mapping = CreateMapping(
                fhirTypeCode,
                primitiveTypeName,
                PrimitiveNamespace,
                CSharpTypeCategory.Primitive,
                isPreviewType: false);
            return true;
        }

        var isPreviewType =
            previewFhirTypeNames?.Contains(fhirTypeCode) == true;
        if (!isPreviewType && !_knownComplexTypeNames.Contains(fhirTypeCode))
        {
            return false;
        }

        var nameResult = _nameConverter.ConvertTypeName(fhirTypeCode);
        if (!nameResult.IsSuccess)
        {
            return false;
        }

        var targetNamespace = isPreviewType
            ? previewNamespace!
            : ComplexTypeNamespace;
        mapping = CreateMapping(
            fhirTypeCode,
            nameResult.Name!,
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
