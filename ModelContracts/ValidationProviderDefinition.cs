using MyFhirSdk.Core;

namespace MyFhirSdk.ModelContracts;

public enum ModelIssueCode { Required, Cardinality, PrimitiveFormat, ChoiceElement, Profile }
public enum ModelIssueSeverity { Information, Warning, Error }
public sealed record ModelValidationNode(FhirObject Value, string Path);
public sealed record ModelValidationIssue(string Path, string Message, ModelIssueCode Code,
    ModelIssueSeverity Severity = ModelIssueSeverity.Error, string? RuleId = null);

/// <summary>A version-owned rule with a neutral signature. Evaluation must be safe for concurrent use.</summary>
public sealed record ValidationRuleDefinition(Type AppliesTo, string RuleId,
    Func<ModelValidationNode, IReadOnlyList<ModelValidationIssue>> Evaluate);

public sealed class ValidationProviderDefinition
{
    public ValidationProviderDefinition(ModelIdentity identity, ModelCapabilities capabilities,
        IEnumerable<ValidationRuleDefinition> rules)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Rules = ModelMetadataDefinition.Freeze(rules);
    }

    public ModelIdentity Identity { get; }
    public ModelCapabilities Capabilities { get; }
    public IReadOnlyList<ValidationRuleDefinition> Rules { get; }
}
