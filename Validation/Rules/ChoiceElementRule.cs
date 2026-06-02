using MyFhirSdk.Core;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal sealed class ChoiceElementRule<TFhirObject> : IFhirValidationRule
    where TFhirObject : FhirObject
{
    private readonly string _choicePath;
    private readonly bool _required;
    private readonly IReadOnlyList<Func<TFhirObject, object?>> _getChoices;

    private ChoiceElementRule(
        string choicePath,
        bool required,
        IReadOnlyList<Func<TFhirObject, object?>> getChoices)
    {
        _choicePath = choicePath;
        _required = required;
        _getChoices = getChoices;
    }

    public static ChoiceElementRule<TFhirObject> AtMostOne(
        string choicePath,
        params Func<TFhirObject, object?>[] getChoices)
    {
        return new ChoiceElementRule<TFhirObject>(choicePath, required: false, getChoices);
    }

    public static ChoiceElementRule<TFhirObject> ExactlyOne(
        string choicePath,
        params Func<TFhirObject, object?>[] getChoices)
    {
        return new ChoiceElementRule<TFhirObject>(choicePath, required: true, getChoices);
    }

    public void Validate(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues)
    {
        if (node.Value is not TFhirObject value)
        {
            return;
        }

        var presentCount = _getChoices.Count(choice => ValidationValuePresence.IsPresent(choice(value)));
        if (presentCount <= 1 && (!_required || presentCount == 1))
        {
            return;
        }

        var path = FhirPathFormatter.Combine(node.Path, _choicePath);
        issues.Add(new ValidationIssue
        {
            Path = path,
            Code = ValidationIssueCode.ChoiceElement,
            Severity = ValidationSeverity.Error,
            Message = _required
                ? path + " requires exactly one value."
                : path + " allows only one value."
        });
    }
}
