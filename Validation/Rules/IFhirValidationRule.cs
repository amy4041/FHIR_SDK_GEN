using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation.Rules;

internal interface IFhirValidationRule
{
    void Validate(FhirObjectGraphNode node, ICollection<ValidationIssue> issues);
}
