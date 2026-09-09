namespace MyFhirSdk.CodeGen.Models;

public sealed record GenerationManifestProvenance(
    int CompatibilitySchemaVersion,
    string CompatibilityVersionPolicy,
    string ToolPackageId,
    string ToolVersion,
    string CodeGenVersion,
    int RuntimeDescriptorSchemaVersion,
    string RuntimeDescriptorVersion,
    string RuntimeDescriptorSha256,
    string CompilerReferenceLogicalIdentity,
    string CompilerReferenceSha256,
    string TargetFramework);
