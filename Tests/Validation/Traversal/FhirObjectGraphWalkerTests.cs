namespace MyFhirSdk.Tests.Validation.Traversal;

public sealed class FhirObjectGraphWalkerTests
{
    [Fact]
    public void ValidateReportsIndexedPathForNestedPrimitive()
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

        Assert.False(result.IsValid);
        ValidationAssert.HasIssue(result, "Patient.name[0].given[0]", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateIgnoresNullOptionalFields()
    {
        var patient = new Patient
        {
            BirthDate = null,
            ManagingOrganization = null
        };
        var validator = new FhirValidator();

        var result = validator.Validate(patient);

        Assert.True(result.IsValid);
    }
}
