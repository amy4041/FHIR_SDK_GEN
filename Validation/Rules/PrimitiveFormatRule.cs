using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal static class PrimitiveFormatRule
{
    private static readonly PrimitiveRegistry Registry = PrimitiveRegistry.Default;

    public static void Validate(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues)
    {
        ValidateId(node, issues);
        ValidatePrimitive(node, issues);
    }

    private static void ValidateId(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues)
    {
        switch (node.Value)
        {
            case Resource resource:
                AddInvalidIdIssueIfNeeded(resource.Id, FhirPathFormatter.Combine(node.Path, "id"), issues);
                break;
            case Element element:
                AddInvalidIdIssueIfNeeded(element.Id, FhirPathFormatter.Combine(node.Path, "id"), issues);
                break;
        }
    }

    private static void AddInvalidIdIssueIfNeeded(
        string? id,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (Registry.GetRequired("id").Validator.IsValidValue(id))
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Path = path,
            Code = ValidationIssueCode.PrimitiveFormat,
            Severity = ValidationSeverity.Error,
            Message = path + " has invalid FHIR id format."
        });
    }

    private static void ValidatePrimitive(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues)
    {
        if (node.Value is not IPrimitiveValueAccessor)
        {
            return;
        }

        var definition = Registry.GetRequired(node.Value.GetType());
        if (definition.Validator.IsValid(node.Value))
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Path = node.Path,
            Code = ValidationIssueCode.PrimitiveFormat,
            Severity = ValidationSeverity.Error,
            Message = node.Path + " has invalid " + node.Value.GetType().Name + " format."
        });
    }
}
