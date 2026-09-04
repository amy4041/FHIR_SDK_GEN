using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Generation;

public sealed record ModelGenerationOptions(
    string PackagePath,
    string OutputPath,
    string PackageId,
    string PackageVersion,
    string FhirVersion,
    string PrimitivePolicyPath,
    string OwnershipPolicyPath,
    ModelIrPolicyPaths ModelIrPolicyPaths,
    string ValidationPolicyPath,
    IReadOnlyList<string> SelectedCanonicals,
    string CodeGenVersion);

