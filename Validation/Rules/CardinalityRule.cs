namespace MyFhirSdk.Validation.Rules;

internal static class CardinalityRule
{
    public static void AddNullListIssue(
        string path,
        ICollection<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue
        {
            Path = path,
            Code = ValidationIssueCode.Cardinality,
            Severity = ValidationSeverity.Error,
            Message = path + " must be a list instance."
        });
    }

    public static void AddNullItemIssue(
        string path,
        ICollection<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue
        {
            Path = path,
            Code = ValidationIssueCode.Cardinality,
            Severity = ValidationSeverity.Error,
            Message = path + " cannot be null."
        });
    }
}
