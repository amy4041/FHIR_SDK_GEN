namespace MyFhirSdk.Tests.Validation.Rules;

public static class RequiredFieldRuleTests
{
    public static void ValidateReportsMissingBundleType()
    {
        var result = new FhirValidator().Validate(new Bundle());

        TestAssert.HasIssue(result, "Bundle.type", ValidationIssueCode.Required);
    }

    public static void ValidateReportsMissingCoverageTopLevelFields()
    {
        var result = new FhirValidator().Validate(new Coverage());

        TestAssert.HasIssue(result, "Coverage.status", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Coverage.kind", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Coverage.beneficiary", ValidationIssueCode.Required);
    }

    public static void ValidateReportsMissingEncounterStatus()
    {
        var result = new FhirValidator().Validate(new Encounter());

        TestAssert.HasIssue(result, "Encounter.status", ValidationIssueCode.Required);
    }

    public static void ValidateReportsMissingClaimTopLevelFields()
    {
        var result = new FhirValidator().Validate(new Claim());

        TestAssert.HasIssue(result, "Claim.status", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Claim.type", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Claim.use", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Claim.patient", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Claim.created", ValidationIssueCode.Required);
    }

    public static void ValidateDoesNotRequirePatientOptionalFields()
    {
        var result = new FhirValidator().Validate(new Patient());

        TestAssert.IsTrue(result.IsValid);
    }

    public static void ValidateReportsMissingBundleLinkFields()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Link = [new BundleLink()]
        };

        var result = new FhirValidator().Validate(bundle);

        TestAssert.HasIssue(result, "Bundle.link[0].relation", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Bundle.link[0].url", ValidationIssueCode.Required);
    }

    public static void ValidateReportsMissingCoverageClassFields()
    {
        var coverage = CreateValidCoverage();
        coverage.Class = [new CoverageClass()];

        var result = new FhirValidator().Validate(coverage);

        TestAssert.HasIssue(result, "Coverage.class[0].type", ValidationIssueCode.Required);
        TestAssert.HasIssue(result, "Coverage.class[0].value", ValidationIssueCode.Required);
    }

    public static void ValidateReportsMissingEncounterLocation()
    {
        var encounter = new Encounter
        {
            Status = new FhirCode("in-progress"),
            Location = [new EncounterLocation()]
        };

        var result = new FhirValidator().Validate(encounter);

        TestAssert.HasIssue(result, "Encounter.location[0].location", ValidationIssueCode.Required);
    }

    private static Coverage CreateValidCoverage()
    {
        return new Coverage
        {
            Status = new FhirCode("active"),
            Kind = new FhirCode("insurance"),
            Beneficiary = new Reference { ReferenceValue = new FhirString("Patient/123") }
        };
    }
}
