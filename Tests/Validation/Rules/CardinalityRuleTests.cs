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

    public static void ValidateReportsEmptyRequiredRepeatedField()
    {
        var claim = new Claim
        {
            Status = new FhirCode("active"),
            Type = new CodeableConcept { Text = new FhirString("Professional") },
            Use = new FhirCode("claim"),
            Patient = new Reference { ReferenceValue = new FhirString("Patient/123") },
            Created = new FhirDateTime("2026-06-05"),
            Item =
            [
                new ClaimItem
                {
                    BodySite = [new ClaimBodySite()]
                }
            ]
        };

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.item[0].bodySite[0].site", ValidationIssueCode.Cardinality);
    }
}
