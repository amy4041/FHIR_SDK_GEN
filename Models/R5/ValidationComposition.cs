using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelSupport;

namespace MyFhirSdk.Validation.Rules;

internal sealed record IFhirValidationRule(ValidationRuleDefinition Definition);
internal static class RequiredFieldRule<T> where T : FhirObject
{
    internal static IFhirValidationRule For(string field, Func<T, object?> get) => new(ModelRules.Required(field, get));
    internal static IFhirValidationRule ForList(string field, Func<T, object?> get) => new(ModelRules.Required(field, get, list: true));
}
internal static class ChoiceElementRule<T> where T : FhirObject
{
    internal static IFhirValidationRule AtMostOne(string field, params Func<T, object?>[] choices) => new(ModelRules.Choice(field, false, choices));
    internal static IFhirValidationRule ExactlyOne(string field, params Func<T, object?>[] choices) => new(ModelRules.Choice(field, true, choices));
}
internal sealed class ResourceRuleRegistry
{
    private ResourceRuleRegistry(IEnumerable<KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>> entries)
        => Definitions = Array.AsReadOnly(entries.SelectMany(e => e.Value.Select(r => r.Definition)).ToArray());
    internal IReadOnlyList<ValidationRuleDefinition> Definitions { get; }
    internal static ResourceRuleRegistry Create(IEnumerable<KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>> entries) => new(entries);
}
