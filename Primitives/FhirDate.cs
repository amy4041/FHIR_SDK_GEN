using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR date primitive. Supports YYYY, YYYY-MM, and YYYY-MM-DD with no timezone.
/// </summary>
public sealed partial class FhirDate : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public FhirDate()
    {
    }

    public FhirDate(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        if (!DateRegex().IsMatch(Value))
        {
            return false;
        }

        if (Value.StartsWith("0000", StringComparison.Ordinal))
        {
            return false;
        }

        return Value.Length switch
        {
            4 => true,
            7 => DateOnly.TryParseExact(Value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            10 => DateOnly.TryParseExact(Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
    }

    [GeneratedRegex(@"^[0-9]{4}(-(0[1-9]|1[0-2])(-(0[1-9]|[12][0-9]|3[01]))?)?$")]
    private static partial Regex DateRegex();
}
