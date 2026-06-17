namespace MyFhirSdk.Tests.Validation;

public static class ValidationResultTests
{
    public static void EmptyIssuesIsValid()
    {
        var result = new ValidationResult(Array.Empty<ValidationIssue>());

        TestAssert.IsTrue(result.IsValid);
        TestAssert.AreEqual(0, result.Issues.Count);
    }

    public static void IssuesMakeResultInvalid()
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

        TestAssert.IsFalse(result.IsValid);
        TestAssert.AreEqual(1, result.Issues.Count);
    }

    public static void PreservesIssueDetails()
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

        TestAssert.AreEqual("Patient.identifier", result.Issues[0].Path);
        TestAssert.AreEqual(ValidationIssueCode.Cardinality, result.Issues[0].Code);
        TestAssert.AreEqual(ValidationSeverity.Error, result.Issues[0].Severity);
        TestAssert.AreEqual("Patient.identifier is required by TW Core Patient.", result.Issues[0].Message);
        TestAssert.AreEqual(ValidationRuleSource.ImplementationGuide, result.Issues[0].Source);
        TestAssert.AreEqual("tw.gov.mohw.twcore#1.0.0", result.Issues[0].PackageId);
        TestAssert.AreEqual(
            "https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore",
            result.Issues[0].ProfileUrl);
        TestAssert.AreEqual("TWCORE-PAT-002", result.Issues[0].RuleId);
    }

    public static void IssueDefaultsToBaseFhirSource()
    {
        var issue = new ValidationIssue();

        TestAssert.AreEqual(ValidationRuleSource.BaseFhir, issue.Source);
        TestAssert.IsNull(issue.PackageId);
        TestAssert.IsNull(issue.ProfileUrl);
        TestAssert.IsNull(issue.RuleId);
    }
}
