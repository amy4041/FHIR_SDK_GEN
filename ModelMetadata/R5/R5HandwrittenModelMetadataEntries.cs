using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

namespace MyFhirSdk.ModelMetadata.R5;

internal static class R5HandwrittenModelMetadataEntries
{
    internal static ImmutableModelMetadataProvider Create() =>
        new(
            CreateResources(),
            CreateConcreteDatatypes(),
            CreateDeclaredDatatypes(),
            CreateExtensionValues());

    private static ResourceTypeMetadata[] CreateResources() =>
    [
        Resource<Bundle>("Bundle"),
        Resource<Claim>("Claim"),
        Resource<Coverage>("Coverage"),
        Resource<Encounter>("Encounter"),
        Resource<Organization>("Organization"),
        Resource<Patient>("Patient"),
        Resource<Practitioner>("Practitioner")
    ];

    private static Type[] CreateConcreteDatatypes() =>
    [
        typeof(Meta),
        typeof(Narrative),
        typeof(Address),
        typeof(Attachment),
        typeof(CodeableConcept),
        typeof(CodeableReference),
        typeof(Coding),
        typeof(ContactPoint),
        typeof(Duration),
        typeof(ExtendedContactDetail),
        typeof(HumanName),
        typeof(Identifier),
        typeof(Money),
        typeof(Period),
        typeof(Quantity),
        typeof(Reference),
        typeof(Signature),
        typeof(SimpleQuantity),
        typeof(VirtualServiceDetail)
    ];

    private static DeclaredDataTypeMetadata[] CreateDeclaredDatatypes() =>
    [
        new(typeof(Meta), "security", typeof(Coding)),
        new(typeof(Meta), "tag", typeof(Coding))
    ];

    private static ExtensionValueMetadata[] CreateExtensionValues() =>
    [
        ExtensionValue<FhirBase64Binary>("valueBase64Binary"),
        ExtensionValue<FhirBoolean>("valueBoolean"),
        ExtensionValue<FhirCanonical>("valueCanonical"),
        ExtensionValue<FhirCode>("valueCode"),
        ExtensionValue<FhirDate>("valueDate"),
        ExtensionValue<FhirDateTime>("valueDateTime"),
        ExtensionValue<FhirDecimal>("valueDecimal"),
        ExtensionValue<FhirId>("valueId"),
        ExtensionValue<FhirInstant>("valueInstant"),
        ExtensionValue<FhirInteger>("valueInteger"),
        ExtensionValue<FhirInteger64>("valueInteger64"),
        ExtensionValue<FhirMarkdown>("valueMarkdown"),
        ExtensionValue<FhirOid>("valueOid"),
        ExtensionValue<FhirPositiveInt>("valuePositiveInt"),
        ExtensionValue<FhirString>("valueString"),
        ExtensionValue<FhirTime>("valueTime"),
        ExtensionValue<FhirUnsignedInt>("valueUnsignedInt"),
        ExtensionValue<FhirUri>("valueUri"),
        ExtensionValue<FhirUrl>("valueUrl"),
        ExtensionValue<FhirUuid>("valueUuid"),
        ExtensionValue<Meta>("valueMeta"),
        ExtensionValue<Narrative>("valueNarrative"),
        ExtensionValue<Address>("valueAddress"),
        ExtensionValue<Attachment>("valueAttachment"),
        ExtensionValue<CodeableConcept>("valueCodeableConcept"),
        ExtensionValue<CodeableReference>("valueCodeableReference"),
        ExtensionValue<Coding>("valueCoding"),
        ExtensionValue<ContactPoint>("valueContactPoint"),
        ExtensionValue<Duration>("valueDuration"),
        ExtensionValue<ExtendedContactDetail>("valueExtendedContactDetail"),
        ExtensionValue<HumanName>("valueHumanName"),
        ExtensionValue<Identifier>("valueIdentifier"),
        ExtensionValue<Money>("valueMoney"),
        ExtensionValue<Period>("valuePeriod"),
        ExtensionValue<Quantity>("valueQuantity"),
        ExtensionValue<Reference>("valueReference"),
        ExtensionValue<Signature>("valueSignature"),
        ExtensionValue<SimpleQuantity>("valueQuantity", isParserTarget: false),
        ExtensionValue<VirtualServiceDetail>("valueVirtualServiceDetail")
    ];

    private static ResourceTypeMetadata Resource<TResource>(string fhirTypeName)
        where TResource : Resource, new() =>
        new(fhirTypeName, typeof(TResource), static () => new TResource());

    private static ExtensionValueMetadata ExtensionValue<TValue>(
        string jsonPropertyName,
        bool isParserTarget = true)
        where TValue : IFhirExtensionValue =>
        new(typeof(TValue), jsonPropertyName, isParserTarget);
}
