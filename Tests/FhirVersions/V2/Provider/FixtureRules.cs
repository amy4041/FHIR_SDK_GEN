using System.Collections;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;

namespace MyFhirSdk.Tests.FhirVersions.V2;

// Test-only executor-independent entries for the V1 bounded model set.
internal static class ResourceRuleRegistry
{
    internal static ValidationProviderDefinition Create(IEnumerable<KeyValuePair<Type, IReadOnlyList<ValidationRuleDefinition>>> entries)
        => new(R4ProviderFixture.Identity, new ModelCapabilities(false, true, true, false, false),
            entries.SelectMany(e => e.Value));
}

internal static class RequiredFieldRule<T> where T : FhirObject
{
    internal static ValidationRuleDefinition For(string field, Func<T, object?> get)
        => Create(field, get, false);
    internal static ValidationRuleDefinition ForList(string field, Func<T, object?> get)
        => Create(field, get, true);
    private static ValidationRuleDefinition Create(string field, Func<T, object?> get, bool list)
        => new(typeof(T), typeof(T).FullName + ":required:" + field, node =>
        {
            if (FixturePresence.IsPresent(get((T)node.Value))) return [];
            var path = node.Path + "." + field;
            return [new ModelValidationIssue(path, list ? path + " must contain at least one item." : path + " is required.",
                list ? ModelIssueCode.Cardinality : ModelIssueCode.Required,
                RuleId: typeof(T).FullName + ":required:" + field)];
        });
}

internal static class ChoiceElementRule<T> where T : FhirObject
{
    internal static ValidationRuleDefinition AtMostOne(string field, params Func<T, object?>[] choices)
        => Create(field, false, choices);
    internal static ValidationRuleDefinition ExactlyOne(string field, params Func<T, object?>[] choices)
        => Create(field, true, choices);
    private static ValidationRuleDefinition Create(string field, bool required, Func<T, object?>[] choices)
        => new(typeof(T), typeof(T).FullName + ":choice:" + field, node =>
        {
            var count = choices.Count(get => FixturePresence.IsPresent(get((T)node.Value)));
            if (count <= 1 && (!required || count == 1)) return [];
            var path = node.Path + "." + field;
            return [new ModelValidationIssue(path, required ? path + " requires exactly one value." : path + " allows only one value.",
                ModelIssueCode.ChoiceElement, RuleId: typeof(T).FullName + ":choice:" + field)];
        });
}

internal static class FixturePresence
{
    internal static bool IsPresent(object? value) => value switch
    {
        null => false,
        string text => text.Length > 0,
        IPrimitiveValueAccessor => value.GetType().GetProperty("HasValue")?.GetValue(value) is true ||
            value is Element { Extension.Count: > 0 },
        IEnumerable items => items.Cast<object?>().Any(item => item is not null),
        _ => true
    };
}

internal sealed class ProviderFixtureException : FormatException
{
    internal ProviderFixtureException(string message) : base(message) { }
    internal ProviderFixtureException(string message, Exception inner) : base(message, inner) { }
}
