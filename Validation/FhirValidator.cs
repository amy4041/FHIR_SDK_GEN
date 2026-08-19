using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Primitives;
using MyFhirSdk.Validation.Rules;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Validation;

/// <summary>
/// Default standalone validator for MVP FHIR resource validation.
/// </summary>
public sealed class FhirValidator : IFhirValidator
{
    private readonly IValidationRuleProvider _ruleProvider;
    private readonly PrimitiveRegistry _primitiveDefinitions;
    private readonly FhirObjectGraphWalker _walker;

    /// <summary>
    /// Creates a validator with the default R5 model and primitive rules.
    /// </summary>
    public FhirValidator()
        : this(
            R5ModelMetadataProvider.Default,
            PrimitiveRegistry.Default,
            new FhirObjectGraphWalker())
    {
    }

    internal FhirValidator(
        IValidationRuleProvider ruleProvider,
        FhirObjectGraphWalker walker)
        : this(ruleProvider, PrimitiveRegistry.Default, walker)
    {
    }

    internal FhirValidator(
        IValidationRuleProvider ruleProvider,
        PrimitiveRegistry primitiveDefinitions,
        FhirObjectGraphWalker walker)
    {
        _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
        _primitiveDefinitions = primitiveDefinitions
            ?? throw new ArgumentNullException(nameof(primitiveDefinitions));
        _walker = walker ?? throw new ArgumentNullException(nameof(walker));
    }

    /// <inheritdoc />
    public ValidationResult Validate(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var issues = new List<ValidationIssue>();

        foreach (var node in _walker.Walk(resource, issues))
        {
            PrimitiveFormatRule.Validate(node, issues, _primitiveDefinitions);

            foreach (var rule in _ruleProvider.GetRules(node.Value.GetType()))
            {
                rule.Validate(node, issues);
            }
        }

        return issues.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(issues);
    }
}
