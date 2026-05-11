using System.Globalization;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR decimal primitive. FHIR decimals are JSON numbers with a decimal
/// representation and constrained precision.
/// </summary>
public sealed partial class FhirDecimal : PrimitiveType<decimal?>
{
    public const int MaxIntegerDigits = 18;
    public const int MaxFractionDigits = 17;
    public const int MaxExponentDigits = 9;

    public FhirDecimal()
    {
    }

    public FhirDecimal(decimal? value)
        : base(value)
    {
    }

    public FhirDecimal(string? value)
    {
        Literal = value;

        if (value is not null &&
            decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            Value = parsedValue;
        }
    }

    /// <summary>
    /// Original FHIR decimal literal, when the value was created from text.
    /// This preserves decimal precision such as trailing zeroes.
    /// </summary>
    public string? Literal { get; }

    public bool IsValid()
    {
        var literal = Literal ?? Value?.ToString(CultureInfo.InvariantCulture);
        return literal is null || DecimalRegex().IsMatch(literal);
    }

    public override string ToString()
    {
        return Literal ?? Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    [GeneratedRegex(@"^-?(0|[1-9][0-9]{0,17})(\.[0-9]{1,17})?([eE][+-]?[0-9]{1,9})?$")]
    private static partial Regex DecimalRegex();
}
