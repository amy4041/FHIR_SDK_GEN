namespace MyFhirSdk.CodeGen.Loading;

public sealed record DefinitionPackageIdentity(
    string PackageId,
    string PackageVersion,
    string PackageType,
    string FhirVersion);
