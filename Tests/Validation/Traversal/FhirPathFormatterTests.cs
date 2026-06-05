namespace MyFhirSdk.Tests.Validation.Traversal;

public static class FhirPathFormatterTests
{
    public static void ValidateUsesJsonPropertyNameAttributeInIssuePath()
    {
        var patient = new Patient
        {
            ManagingOrganization = new Reference
            {
                ReferenceValue = new FhirString("")
            }
        };
        var validator = new FhirValidator();

        var result = validator.Validate(patient);

        TestAssert.IsFalse(result.IsValid);
        TestAssert.HasIssue(result, "Patient.managingOrganization.reference", ValidationIssueCode.PrimitiveFormat);
    }
}
