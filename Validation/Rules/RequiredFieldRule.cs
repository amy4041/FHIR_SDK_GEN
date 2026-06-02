using MyFhirSdk.Core;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal sealed class RequiredFieldRule<TFhirObject> : IFhirValidationRule
    where TFhirObject : FhirObject
{
    private readonly string _fieldName;
    private readonly Func<TFhirObject, object?> _getValue;

    private RequiredFieldRule(
        string fieldName,
        Func<TFhirObject, object?> getValue)
    {
        _fieldName = fieldName;
        _getValue = getValue;
    }

    public static RequiredFieldRule<TFhirObject> For(
        string fieldName,
        Func<TFhirObject, object?> getValue)
    {
        return new RequiredFieldRule<TFhirObject>(fieldName, getValue);
    }

    public void Validate(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues)
    {
        if (node.Value is not TFhirObject value)
        {
            return;
        }

        if (ValidationValuePresence.IsPresent(_getValue(value)))
        {
            return;
        }

        var path = FhirPathFormatter.Combine(node.Path, _fieldName);
        issues.Add(new ValidationIssue
        {
            Path = path,
            Code = ValidationIssueCode.Required,
            Severity = ValidationSeverity.Error,
            Message = path + " is required."
        });
    }
}
