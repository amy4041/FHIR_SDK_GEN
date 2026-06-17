namespace MyFhirSdk.Tests.Validation;

internal static class ValidationAssert
{
    public static void HasIssue(
        ValidationResult result,
        string path,
        ValidationIssueCode code,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        var hasIssue = result.Issues.Any(issue =>
            issue.Path == path &&
            issue.Code == code &&
            issue.Severity == severity);

        if (hasIssue)
        {
            return;
        }

        var actualIssues = string.Join(
            Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Code}/{issue.Severity}/{issue.Path}: {issue.Message}"));

        Assert.Fail($"""
            Expected issue {code}/{severity}/{path}, but it was not found.
            Actual issues:
            {actualIssues}
            """);
    }
}
