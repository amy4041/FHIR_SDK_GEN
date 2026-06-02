using MyFhirSdk.Core;
using MyFhirSdk.Validation.Rules;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation;

/// <summary>
/// Default standalone validator for MVP FHIR resource validation.
/// </summary>
public sealed class FhirValidator : IFhirValidator
{
    private readonly ResourceRuleRegistry _ruleRegistry;
    private readonly FhirObjectGraphWalker _walker;

    /// <summary>
    /// Creates a validator with the default MVP rule registry.
    /// </summary>
    public FhirValidator()
        : this(ResourceRuleRegistry.CreateDefault(), new FhirObjectGraphWalker())
    {
    }

    internal FhirValidator(
        ResourceRuleRegistry ruleRegistry,
        FhirObjectGraphWalker walker)
    {
        _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
        _walker = walker ?? throw new ArgumentNullException(nameof(walker));
    }

    /// <inheritdoc />
    public ValidationResult Validate(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var issues = new List<ValidationIssue>();

        foreach (var node in _walker.Walk(resource, issues))
        {
            PrimitiveFormatRule.Validate(node, issues);

            foreach (var rule in _ruleRegistry.GetRules(node.Value.GetType()))
            {
                rule.Validate(node, issues);
            }
        }

        return issues.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(issues);
    }
}
