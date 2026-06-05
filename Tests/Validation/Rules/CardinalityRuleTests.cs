namespace MyFhirSdk.Tests.Validation.Rules;

public static class CardinalityRuleTests
{
    public static void ValidateReportsNullRepeatedField()
    {
        var patient = new Patient
        {
            Name = null!
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.name", ValidationIssueCode.Cardinality);
    }

    public static void ValidateReportsNullRepeatedItem()
    {
        var patient = new Patient
        {
            Name = [null!]
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.name[0]", ValidationIssueCode.Cardinality);
    }
}
