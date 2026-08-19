namespace MyFhirSdk.Validation.Rules;

internal interface IValidationRuleProvider
{
    IReadOnlyList<IFhirValidationRule> GetRules(Type type);
}
