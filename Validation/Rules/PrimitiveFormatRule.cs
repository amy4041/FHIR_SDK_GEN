using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal static class PrimitiveFormatRule
{
    public static void Validate(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues,
        PrimitiveRegistry primitiveDefinitions)
    {
        ArgumentNullException.ThrowIfNull(primitiveDefinitions);
        ValidateId(node, issues, primitiveDefinitions);
        ValidatePrimitive(node, issues, primitiveDefinitions);
    }

    private static void ValidateId(
        FhirObjectGraphNode node,
        ICollection<ValidationIssue> issues,
        PrimitiveRegistry primitiveDefinitions)
    {
        switch (node.Value)
        {
            case Resource resource:
                AddInvalidIdIssueIfNeeded(
                    resource.Id,
                    FhirPathFormatter.Combine(node.Path, "id"),
                    issues,
                    primitiveDefinitions);
                break;
            case Element element:
                AddInvalidIdIssueIfNeeded(
                    element.Id,
                    FhirPathFormatter.Combine(node.Path, "id"),
                    issues,
                    primitiveDefinitions);
                break;
        }
    }

    private static void AddInvalidIdIssueIfNeeded(
        string? id,
        string path,
        ICollection<ValidationIssue> issues,
        PrimitiveRegistry primitiveDefinitions)
    {
        if (primitiveDefinitions.GetRequired("id").Validator.IsValidValue(id))
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
        ICollection<ValidationIssue> issues,
        PrimitiveRegistry primitiveDefinitions)
    {
        if (node.Value is not IPrimitiveValueAccessor)
        {
            return;
        }

        var definition = primitiveDefinitions.GetRequired(node.Value.GetType());
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
