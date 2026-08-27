namespace MyFhirSdk.CodeGen.Generation;

public sealed record GeneratorOptions(
    string InputPath,
    string OutputPath,
    string TargetNamespace,
    string FhirVersion,
    IReadOnlyList<string> TypeNames,
    string PrimitivePolicyPath);
