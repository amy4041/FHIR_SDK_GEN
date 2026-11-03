using System.Globalization;
using System.Text.RegularExpressions;
using MyFhirSdk.ModelContracts;

namespace MyFhirSdk.ModelSupport;

/// <summary>Model-neutral lexical validation; release providers supply patterns and normalization.</summary>
public static class PrimitiveValidationPolicies
{
    /// <summary>Compose a full-string lexical rule with optional semantic validation.
    /// Normalization applies only to semantic checks; the original model value is retained.</summary>
    public static IPrimitiveValidator WithPattern(string pattern, IPrimitiveValidator? semanticValidator = null,
        Func<string, string>? normalizeForValidation = null, bool useLiteral = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var regex = new Regex("\\A(?:" + pattern + ")\\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        return new PrimitiveValidator(primitive =>
        {
            var literal = useLiteral ? primitive.GetType().GetProperty("Literal")?.GetValue(primitive) as string : null;
            return literal ?? PrimitiveValueAccess.GetAccessor(primitive).UntypedValue;
        }, value =>
        {
            if (value is null) return true;
            var text = value switch
            {
                string literal => literal,
                IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
                _ => null
            };
            return text is not null && regex.IsMatch(text) &&
                (semanticValidator is null || semanticValidator.IsValidValue(normalizeForValidation?.Invoke(text) ?? text));
        });
    }
}
