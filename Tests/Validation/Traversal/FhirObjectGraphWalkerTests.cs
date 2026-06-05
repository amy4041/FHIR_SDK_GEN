namespace MyFhirSdk.Tests.Validation.Traversal;

public static class FhirObjectGraphWalkerTests
{
    public static void ValidateReportsIndexedPathForNestedPrimitive()
    {
        var patient = new Patient
        {
            Name =
            [
                new HumanName
                {
                    Given = [new FhirString("")]
                }
            ]
        };
        var validator = new FhirValidator();

        var result = validator.Validate(patient);

        TestAssert.IsFalse(result.IsValid);
        TestAssert.HasIssue(result, "Patient.name[0].given[0]", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateIgnoresNullOptionalFields()
    {
        var patient = new Patient
        {
            BirthDate = null,
            ManagingOrganization = null
        };
        var validator = new FhirValidator();

        var result = validator.Validate(patient);

        TestAssert.IsTrue(result.IsValid);
    }
}
