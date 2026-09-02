namespace MyFhirSdk.CodeGen.Loading;

public sealed record DefinitionPackageLoadOptions(
    string PackageId,
    string PackageVersion,
    string FhirVersion,
    string PackageType = "Core");
