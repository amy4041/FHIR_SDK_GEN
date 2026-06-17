namespace MyFhirSdk.Tests.Validation.Traversal;

public sealed class FhirPathFormatterTests
{
    [Fact]
    public void ValidateUsesJsonPropertyNameAttributeInIssuePath()
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

        Assert.False(result.IsValid);
        ValidationAssert.HasIssue(result, "Patient.managingOrganization.reference", ValidationIssueCode.PrimitiveFormat);
    }
}
