using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Models;

public sealed record PrimitiveWrapperModel
{
    public PrimitiveWrapperModel(
        string fhirTypeName,
        string canonical,
        string fhirVersion,
        string @namespace,
        string wrapperName,
        string clrValueType,
        string documentation,
        PrimitiveWrapperLiteralKind literalKind,
        string? literalPropertyName,
        PrimitiveWrapperToStringKind toStringKind,
        IEnumerable<PrimitiveWrapperConstantModel> publicConstants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonical);
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(wrapperName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clrValueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentation);
        ArgumentNullException.ThrowIfNull(publicConstants);

        if (literalKind == PrimitiveWrapperLiteralKind.None &&
            literalPropertyName is not null)
        {
            throw new ArgumentException(
                "A non-literal primitive wrapper cannot define a literal property.",
                nameof(literalPropertyName));
        }

        if (literalKind != PrimitiveWrapperLiteralKind.None)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(literalPropertyName);
        }

        FhirTypeName = fhirTypeName;
        Canonical = canonical;
        FhirVersion = fhirVersion;
        Namespace = @namespace;
        WrapperName = wrapperName;
        ClrValueType = clrValueType;
        Documentation = documentation;
        LiteralKind = literalKind;
        LiteralPropertyName = literalPropertyName;
        ToStringKind = toStringKind;
        PublicConstants = new ReadOnlyCollection<PrimitiveWrapperConstantModel>(
            publicConstants.ToArray());
    }

    public string FhirTypeName { get; }

    public string Canonical { get; }

    public string FhirVersion { get; }

    public string Namespace { get; }

    public string WrapperName { get; }

    public string ClrValueType { get; }

    public string Documentation { get; }

    public PrimitiveWrapperLiteralKind LiteralKind { get; }

    public string? LiteralPropertyName { get; }

    public PrimitiveWrapperToStringKind ToStringKind { get; }

    public IReadOnlyList<PrimitiveWrapperConstantModel> PublicConstants { get; }

    public string FileName => $"{WrapperName}.g.cs";
}

public sealed record PrimitiveWrapperConstantModel(
    string Name,
    PrimitiveWrapperConstantClrType ClrType,
    long Value);

public enum PrimitiveWrapperLiteralKind
{
    None,
    Decimal,
    Integer64
}

public enum PrimitiveWrapperToStringKind
{
    Inherited,
    BooleanLowercase,
    InvariantValue,
    LiteralOrInvariantValue
}

public enum PrimitiveWrapperConstantClrType
{
    Int32,
    Int64
}
