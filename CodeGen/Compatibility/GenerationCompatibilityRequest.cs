namespace MyFhirSdk.CodeGen.Compatibility;

public sealed record GenerationCompatibilityRequest(
    string CodeGenVersion,
    string FhirPackageId,
    string FhirPackageVersion,
    string FhirVersion,
    string PrimitivePolicyPath,
    IReadOnlyList<GenerationPolicyAsset> ModelPolicies)
{
    public static GenerationCompatibilityRequest Primitive(
        string codeGenVersion,
        string fhirPackageId,
        string fhirPackageVersion,
        string fhirVersion,
        string primitivePolicyPath) =>
        new(
            codeGenVersion,
            fhirPackageId,
            fhirPackageVersion,
            fhirVersion,
            primitivePolicyPath,
            Array.Empty<GenerationPolicyAsset>());
}

public sealed record GenerationPolicyAsset(string LogicalName, string Path);
