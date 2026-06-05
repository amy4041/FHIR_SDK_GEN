using MyFhirSdk.Core;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal sealed class RequiredFieldRule<TFhirObject> : IFhirValidationRule
    where TFhirObject : FhirObject
{
    private readonly string _fieldName;
    private readonly Func<TFhirObject, object?> _getValue;
    private readonly bool _requiresListItem;

    private RequiredFieldRule(
        string fieldName,
        Func<TFhirObject, object?> getValue,
        bool requiresListItem)
    {
        _fieldName = fieldName;
        _getValue = getValue;
        _requiresListItem = requiresListItem;
    }

    public static RequiredFieldRule<TFhirObject> For(
        string fieldName,
        Func<TFhirObject, object?> getValue)
    {
        return new RequiredFieldRule<TFhirObject>(fieldName, getValue, requiresListItem: false);
    }

    public static RequiredFieldRule<TFhirObject> ForList(
        string fieldName,
        Func<TFhirObject, object?> getValue)
    {
        return new RequiredFieldRule<TFhirObject>(fieldName, getValue, requiresListItem: true);
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
            Code = _requiresListItem ? ValidationIssueCode.Cardinality : ValidationIssueCode.Required,
            Severity = ValidationSeverity.Error,
            Message = _requiresListItem
                ? path + " must contain at least one item."
                : path + " is required."
        });
    }
}
