namespace MyFhirSdk.Tests.Validation;

public sealed class ValidationResultTests
{
    [Fact]
    public void EmptyIssuesIsValid()
    {
        var result = new ValidationResult(Array.Empty<ValidationIssue>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void IssuesMakeResultInvalid()
    {
        var result = new ValidationResult(
        [
            new ValidationIssue
            {
                Path = "Patient.birthDate",
                Code = ValidationIssueCode.PrimitiveFormat,
                Severity = ValidationSeverity.Error,
                Message = "Invalid date."
            }
        ]);

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
    }

    [Fact]
    public void PreservesIssueDetails()
    {
        var issue = new ValidationIssue
        {
            Path = "Patient.identifier",
            Code = ValidationIssueCode.Cardinality,
            Severity = ValidationSeverity.Error,
            Message = "Patient.identifier is required by TW Core Patient.",
            Source = ValidationRuleSource.ImplementationGuide,
            PackageId = "tw.gov.mohw.twcore#1.0.0",
            ProfileUrl = "https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore",
            RuleId = "TWCORE-PAT-002"
        };
        var result = new ValidationResult([issue]);

        Assert.Equal("Patient.identifier", result.Issues[0].Path);
        Assert.Equal(ValidationIssueCode.Cardinality, result.Issues[0].Code);
        Assert.Equal(ValidationSeverity.Error, result.Issues[0].Severity);
        Assert.Equal("Patient.identifier is required by TW Core Patient.", result.Issues[0].Message);
        Assert.Equal(ValidationRuleSource.ImplementationGuide, result.Issues[0].Source);
        Assert.Equal("tw.gov.mohw.twcore#1.0.0", result.Issues[0].PackageId);
        Assert.Equal(
            "https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore",
            result.Issues[0].ProfileUrl);
        Assert.Equal("TWCORE-PAT-002", result.Issues[0].RuleId);
    }

    [Fact]
    public void IssueDefaultsToBaseFhirSource()
    {
        var issue = new ValidationIssue();

        Assert.Equal(ValidationRuleSource.BaseFhir, issue.Source);
        Assert.Null(issue.PackageId);
        Assert.Null(issue.ProfileUrl);
        Assert.Null(issue.RuleId);
    }
}
