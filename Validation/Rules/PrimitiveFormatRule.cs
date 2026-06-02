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
        if (new FhirId(id).IsValid())
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
        if (!IsPrimitiveType(node.Value.GetType()))
        {
            return;
        }

        var isValidMethod = node.Value.GetType().GetMethod(nameof(FhirId.IsValid), Type.EmptyTypes);
        if (isValidMethod?.ReturnType != typeof(bool))
        {
            return;
        }

        if (isValidMethod.Invoke(node.Value, Array.Empty<object>()) is true)
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

    private static bool IsPrimitiveType(Type? type)
    {
        while (type is not null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PrimitiveType<>))
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }
}
