namespace MyFhirSdk.Tests.ImplementationGuides.TwCore.Validation;

public sealed class TwCorePatientValidationTests
{
    [Fact]
    public void EmptyPatientRemainsValidForBaseFhirButInvalidForTwCorePatient()
    {
        var patient = new Patient();

        var baseResult = new FhirValidator().Validate(patient);
        var profileResult = CreateValidator().Validate(patient, TwCoreProfiles.Patient);

        Assert.True(baseResult.IsValid);
        var issue = Assert.Single(profileResult.Issues);
        AssertIssue(
            issue,
            "Patient.identifier",
            ValidationIssueCode.Cardinality,
            "TWCORE-PAT-002");
    }

    [Fact]
    public void ValidateReportsMissingIdentifierSystem()
    {
        var patient = new Patient
        {
            Identifier =
            [
                new Identifier
                {
                    Value = new FhirString("A123456789")
                }
            ]
        };

        var result = CreateValidator().Validate(patient, TwCoreProfiles.Patient);

        var issue = Assert.Single(result.Issues);
        AssertIssue(
            issue,
            "Patient.identifier[0].system",
            ValidationIssueCode.Required,
            "TWCORE-PAT-003");
    }

    [Fact]
    public void ValidateReportsMissingIdentifierValue()
    {
        var patient = new Patient
        {
            Identifier =
            [
                new Identifier
                {
                    System = new FhirUri("https://www.moi.gov.tw/")
                }
            ]
        };

        var result = CreateValidator().Validate(patient, TwCoreProfiles.Patient);

        var issue = Assert.Single(result.Issues);
        AssertIssue(
            issue,
            "Patient.identifier[0].value",
            ValidationIssueCode.Required,
            "TWCORE-PAT-004");
    }

    [Fact]
    public void ValidateReportsMissingSystemAndValueForEachIdentifier()
    {
        var patient = new Patient
        {
            Identifier =
            [
                new Identifier(),
                new Identifier()
            ]
        };

        var result = CreateValidator().Validate(patient, TwCoreProfiles.Patient);

        Assert.Equal(4, result.Issues.Count);
        Assert.Contains(result.Issues, issue =>
            issue.Path == "Patient.identifier[0].system" && issue.RuleId == "TWCORE-PAT-003");
        Assert.Contains(result.Issues, issue =>
            issue.Path == "Patient.identifier[0].value" && issue.RuleId == "TWCORE-PAT-004");
        Assert.Contains(result.Issues, issue =>
            issue.Path == "Patient.identifier[1].system" && issue.RuleId == "TWCORE-PAT-003");
        Assert.Contains(result.Issues, issue =>
            issue.Path == "Patient.identifier[1].value" && issue.RuleId == "TWCORE-PAT-004");
    }

    [Fact]
    public void ValidatePassesWhenIdentifierHasSystemAndValue()
    {
        var patient = new Patient
        {
            Identifier =
            [
                new Identifier
                {
                    System = new FhirUri("https://www.moi.gov.tw/"),
                    Value = new FhirString("A123456789")
                }
            ]
        };

        var result = CreateValidator().Validate(patient, TwCoreProfiles.Patient);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    private static ProfileValidator CreateValidator()
    {
        return new ProfileValidator(new FhirValidator(), TwCorePackage.Default);
    }

    private static void AssertIssue(
        ValidationIssue issue,
        string path,
        ValidationIssueCode code,
        string ruleId)
    {
        Assert.Equal(path, issue.Path);
        Assert.Equal(code, issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationRuleSource.ImplementationGuide, issue.Source);
        Assert.Equal("tw.gov.mohw.twcore#1.0.0", issue.PackageId);
        Assert.Equal(TwCoreProfiles.Patient, issue.ProfileUrl);
        Assert.Equal(ruleId, issue.RuleId);
    }
}
