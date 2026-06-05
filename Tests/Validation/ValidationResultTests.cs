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
            Path = "Claim.status",
            Code = ValidationIssueCode.Required,
            Severity = ValidationSeverity.Error,
            Message = "Claim.status is required."
        };
        var result = new ValidationResult([issue]);

        TestAssert.AreEqual("Claim.status", result.Issues[0].Path);
        TestAssert.AreEqual(ValidationIssueCode.Required, result.Issues[0].Code);
        TestAssert.AreEqual(ValidationSeverity.Error, result.Issues[0].Severity);
        TestAssert.AreEqual("Claim.status is required.", result.Issues[0].Message);
    }
}
