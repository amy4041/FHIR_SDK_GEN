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
}
