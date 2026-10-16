using System.Collections;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;

namespace MyFhirSdk.ModelSupport;

/// <summary>Shared base-FHIR rule implementations; providers select fields and release-specific entries.</summary>
public static class ModelRules
{
    public static ValidationRuleDefinition Required<T>(string field, Func<T, object?> get, bool list = false) where T : FhirObject
        => new(typeof(T), typeof(T).FullName + ":required:" + field, node =>
        {
            if (IsPresent(get((T)node.Value))) return [];
            var path = node.Path + "." + field;
            return [new ModelValidationIssue(path, list ? path + " must contain at least one item." : path + " is required.",
                list ? ModelIssueCode.Cardinality : ModelIssueCode.Required)];
        });

    public static ValidationRuleDefinition Choice<T>(string field, bool required, params Func<T, object?>[] choices) where T : FhirObject
    {
        var selectors = choices.ToArray();
        return new(typeof(T), typeof(T).FullName + ":choice:" + field, node =>
        {
            var count = selectors.Count(get => IsPresent(get((T)node.Value)));
            if (count <= 1 && (!required || count == 1)) return [];
            var path = node.Path + "." + field;
            return [new ModelValidationIssue(path, required ? path + " requires exactly one value." : path + " allows only one value.",
                ModelIssueCode.ChoiceElement)];
        });
    }

    private static bool IsPresent(object? value) => value switch
    {
        null => false,
        string text => text.Length > 0,
        IPrimitiveValueAccessor => value.GetType().GetProperty("HasValue")?.GetValue(value) is true || value is Element { Extension.Count: > 0 },
        IEnumerable items => items.Cast<object?>().Any(item => item is not null),
        _ => true
    };
}
