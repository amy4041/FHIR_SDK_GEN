namespace MyFhirSdk.CodeGen.Contracts;

public static class RuntimeContractRoles
{
    public const string ModelRoot = "model-root";
    public const string ExtensionValueMarker = "extension-value-marker";
    public const string FoundationBase = "foundation-base";
    public const string ElementFoundation = "element-foundation";
    public const string BackboneElementFoundation = "backbone-element-foundation";
    public const string BackboneTypeFoundation = "backbone-type-foundation";
    public const string DatatypeFoundation = "datatype-foundation";
    public const string PrimitiveWrapperBase = "primitive-wrapper-base";
    public const string ResourceFoundation = "resource-foundation";
    public const string DomainResourceFoundation = "domain-resource-foundation";
    public const string ExtensionBootstrap = "extension-bootstrap";
    public const string MetaBootstrap = "meta-bootstrap";
    public const string NarrativeBootstrap = "narrative-bootstrap";

    public const string DeclaredDatatypeSlot = "declared-datatype";
    public const string ExtensionValueSlot = "extension-value";

    internal static readonly IReadOnlySet<string> SymbolRoles = new HashSet<string>(
    [
        ModelRoot,
        ExtensionValueMarker,
        FoundationBase,
        ElementFoundation,
        BackboneElementFoundation,
        BackboneTypeFoundation,
        DatatypeFoundation,
        PrimitiveWrapperBase,
        ResourceFoundation,
        DomainResourceFoundation,
        ExtensionBootstrap,
        MetaBootstrap,
        NarrativeBootstrap
    ], StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> SlotRoles = new HashSet<string>(
    [
        DeclaredDatatypeSlot,
        ExtensionValueSlot
    ], StringComparer.Ordinal);
}
