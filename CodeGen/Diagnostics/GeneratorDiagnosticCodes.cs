namespace MyFhirSdk.CodeGen.Diagnostics;

public static class GeneratorDiagnosticCodes
{
    public const string InvalidInput = "FSG0001";
    public const string FhirVersionMismatch = "FSG0002";
    public const string MissingSnapshot = "FSG0003";
    public const string MissingDifferential = "FSG0004";
    public const string UnsupportedDefinition = "FSG0005";
    public const string UnsupportedSlicing = "FSG0006";
    public const string UnsupportedChoiceType = "FSG0007";
    public const string UnsupportedContentReference = "FSG0008";
    public const string MissingTypeMapping = "FSG0009";
    public const string CSharpNameConflict = "FSG0010";
    public const string UnsafeOutputPath = "FSG0011";
    public const string CompilationFailure = "FSG0012";
    public const string PrimitivePolicyReadFailure = "FSG0013";
    public const string UnsupportedPrimitivePolicySchema = "FSG0014";
    public const string InvalidPrimitivePolicy = "FSG0015";
    public const string DuplicatePrimitivePolicyEntry = "FSG0016";
    public const string UnknownPrimitivePolicyKey = "FSG0017";
    public const string InvalidPrimitiveLiteralPolicy = "FSG0018";
    public const string InvalidPrimitiveInventory = "FSG0019";
    public const string DuplicatePrimitiveInventoryEntry = "FSG0020";
    public const string MissingPrimitivePolicyEntry = "FSG0021";
    public const string ExtraPrimitivePolicyEntry = "FSG0022";
    public const string PrimitivePolicyIdentityMismatch = "FSG0023";
    public const string InvalidPrimitiveWrapperModel = "FSG0024";
    public const string InvalidPrimitiveRegistryModel = "FSG0025";
    public const string DefinitionPackageReadFailure = "FSG0026";
    public const string DefinitionPackageIdentityMismatch = "FSG0027";
    public const string InvalidDefinitionInventory = "FSG0028";
    public const string DuplicateDefinitionInventoryEntry = "FSG0029";
    public const string ModelOwnershipPolicyReadFailure = "FSG0030";
    public const string InvalidDependencyGraph = "FSG0031";
    public const string MissingDependency = "FSG0032";
    public const string IncompatibleInheritance = "FSG0033";
    public const string InheritanceCycle = "FSG0034";
    public const string UnsupportedPrimitiveReference = "FSG0035";
    public const string InvalidGenerationScope = "FSG0036";
    public const string ModelIrPolicyReadFailure = "FSG0037";
    public const string InvalidModelIr = "FSG0038";
    public const string UnsupportedModelShape = "FSG0039";
    public const string ModelIrCollision = "FSG0040";
}
