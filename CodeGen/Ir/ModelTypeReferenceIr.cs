using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelTypeReferenceIr
{
    internal ModelTypeReferenceIr(
        string fhirTypeCode,
        string targetCanonical,
        string? targetElementId,
        string? clrType,
        bool isAbstractTarget,
        bool isExternal,
        bool isPrimitive,
        bool isSupported,
        IEnumerable<string> profiles,
        IEnumerable<string> targetProfiles)
    {
        FhirTypeCode = fhirTypeCode;
        TargetCanonical = targetCanonical;
        TargetElementId = targetElementId;
        ClrType = clrType;
        IsAbstractTarget = isAbstractTarget;
        IsExternal = isExternal;
        IsPrimitive = isPrimitive;
        IsSupported = isSupported;
        Profiles = new ReadOnlyCollection<string>(profiles.ToArray());
        TargetProfiles = new ReadOnlyCollection<string>(targetProfiles.ToArray());
    }

    public string FhirTypeCode { get; }

    public string TargetCanonical { get; }

    public string? TargetElementId { get; }

    public string? ClrType { get; }

    public bool IsAbstractTarget { get; }

    public bool IsExternal { get; }

    public bool IsPrimitive { get; }

    public bool IsSupported { get; }

    public IReadOnlyList<string> Profiles { get; }

    public IReadOnlyList<string> TargetProfiles { get; }
}
