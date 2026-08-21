using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Policy;

public sealed class ValidatedPrimitiveGenerationPolicy
{
    internal ValidatedPrimitiveGenerationPolicy(
        string sourceFile,
        int schemaVersion,
        string policyVersion,
        string fhirVersion,
        string runtimeContractVersion,
        string primitiveNamespace,
        IEnumerable<ValidatedPrimitivePolicyEntry> primitives)
    {
        SourceFile = sourceFile;
        SchemaVersion = schemaVersion;
        PolicyVersion = policyVersion;
        FhirVersion = fhirVersion;
        RuntimeContractVersion = runtimeContractVersion;
        PrimitiveNamespace = primitiveNamespace;
        Primitives = new ReadOnlyCollection<ValidatedPrimitivePolicyEntry>(
            primitives.ToArray());
    }

    public string SourceFile { get; }

    public int SchemaVersion { get; }

    public string PolicyVersion { get; }

    public string FhirVersion { get; }

    public string RuntimeContractVersion { get; }

    public string PrimitiveNamespace { get; }

    public IReadOnlyList<ValidatedPrimitivePolicyEntry> Primitives { get; }
}

public sealed class ValidatedPrimitivePolicyEntry
{
    internal ValidatedPrimitivePolicyEntry(
        string fhirTypeName,
        string canonical,
        string fhirVersion,
        PrimitiveSupportStatus supportStatus,
        string? unsupportedReason,
        string? wrapperName,
        string? clrValueType,
        PrimitiveJsonToken? jsonToken,
        PrimitiveCodecKey? codecKey,
        PrimitiveValidatorKey? validatorKey,
        bool preserveLiteral,
        bool literalConstructor,
        string? literalPropertyName,
        PrimitiveToStringBehavior? toStringBehavior,
        IEnumerable<PrimitivePublicConstant> publicConstants)
    {
        FhirTypeName = fhirTypeName;
        Canonical = canonical;
        FhirVersion = fhirVersion;
        SupportStatus = supportStatus;
        UnsupportedReason = unsupportedReason;
        WrapperName = wrapperName;
        ClrValueType = clrValueType;
        JsonToken = jsonToken;
        CodecKey = codecKey;
        ValidatorKey = validatorKey;
        PreserveLiteral = preserveLiteral;
        LiteralConstructor = literalConstructor;
        LiteralPropertyName = literalPropertyName;
        ToStringBehavior = toStringBehavior;
        PublicConstants = new ReadOnlyCollection<PrimitivePublicConstant>(
            publicConstants.ToArray());
    }

    public string FhirTypeName { get; }

    public string Canonical { get; }

    public string FhirVersion { get; }

    public PrimitiveSupportStatus SupportStatus { get; }

    public string? UnsupportedReason { get; }

    public string? WrapperName { get; }

    public string? ClrValueType { get; }

    public PrimitiveJsonToken? JsonToken { get; }

    public PrimitiveCodecKey? CodecKey { get; }

    public PrimitiveValidatorKey? ValidatorKey { get; }

    public bool PreserveLiteral { get; }

    public bool LiteralConstructor { get; }

    public string? LiteralPropertyName { get; }

    public PrimitiveToStringBehavior? ToStringBehavior { get; }

    public IReadOnlyList<PrimitivePublicConstant> PublicConstants { get; }

    public bool IsSupported => SupportStatus == PrimitiveSupportStatus.Supported;
}

public sealed record PrimitivePublicConstant(
    string Name,
    PrimitiveConstantClrType ClrType,
    long Value);

public enum PrimitiveSupportStatus
{
    Supported,
    Unsupported
}

public enum PrimitiveJsonToken
{
    String,
    Boolean,
    Number
}

public enum PrimitiveCodecKey
{
    String,
    Boolean,
    Integer,
    DecimalLiteral,
    Integer64Literal
}

public enum PrimitiveValidatorKey
{
    Base64Binary,
    Boolean,
    Canonical,
    Code,
    Date,
    DateTime,
    Decimal,
    Id,
    Instant,
    Integer,
    Integer64,
    Markdown,
    PositiveInt,
    String,
    UnsignedInt,
    Uri,
    Url
}

public enum PrimitiveToStringBehavior
{
    Inherited,
    BooleanLowercase,
    InvariantValue,
    LiteralOrInvariantValue
}

public enum PrimitiveConstantClrType
{
    Int32,
    Int64
}
