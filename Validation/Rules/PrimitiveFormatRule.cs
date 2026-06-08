using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal static class PrimitiveFormatRule
{
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
        if (((IFhirValidatablePrimitive)new FhirId(id)).IsValid())
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
        if (node.Value is not IFhirValidatablePrimitive primitive)
        {
            return;
        }

        if (primitive.IsValid())
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
