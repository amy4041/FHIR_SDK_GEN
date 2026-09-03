using System.Collections.ObjectModel;

namespace MyFhirSdk.Validation.Rules;

internal sealed class ResourceRuleRegistry : IValidationRuleProvider
{
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<IFhirValidationRule>> _rules;

    private ResourceRuleRegistry(
        IReadOnlyDictionary<Type, IReadOnlyList<IFhirValidationRule>> rules)
    {
        _rules = rules;
    }

    internal static ResourceRuleRegistry Create(
        IEnumerable<KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var rules = new Dictionary<Type, IReadOnlyList<IFhirValidationRule>>();
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Key);
            ArgumentNullException.ThrowIfNull(entry.Value);

            if (!rules.TryAdd(entry.Key, entry.Value.ToArray()))
            {
                throw new InvalidOperationException(
                    $"Duplicate validation rule metadata for '{entry.Key.FullName}'.");
            }
        }

        return new ResourceRuleRegistry(
            new ReadOnlyDictionary<Type, IReadOnlyList<IFhirValidationRule>>(rules));
    }

    public IReadOnlyList<IFhirValidationRule> GetRules(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var hierarchy = new Stack<Type>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            hierarchy.Push(current);
        }
        var result = new List<IFhirValidationRule>();
        while (hierarchy.Count > 0)
        {
            if (_rules.TryGetValue(hierarchy.Pop(), out var rules))
            {
                result.AddRange(rules);
            }
        }
        return result;
    }
}
