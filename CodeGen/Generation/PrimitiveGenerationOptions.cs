namespace MyFhirSdk.CodeGen.Generation;

public sealed record PrimitiveGenerationOptions(
    string DefinitionsPath,
    string PolicyPath,
    string OutputPath,
    string FhirVersion,
    string FhirPackageId,
    string FhirPackageVersion,
    string CodeGenVersion,
    string FhirSpecification = "FHIR R5");
