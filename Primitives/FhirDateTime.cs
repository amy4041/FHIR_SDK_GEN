using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR dateTime primitive. Supports partial dates and full date-times.
/// If a time is present, a timezone offset is required.
/// </summary>
public sealed partial class FhirDateTime : PrimitiveType<string>
{
    public FhirDateTime()
    {
    }

    public FhirDateTime(string? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        if (!DateTimeRegex().IsMatch(Value))
        {
            return false;
        }

        if (Value.StartsWith("0000", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Value.Contains('T'))
        {
            return ValidatePartialDate(Value);
        }

        return TryParseDateTimeOffsetAllowingLeapSecond(Value);
    }

    private static bool ValidatePartialDate(string value)
    {
        return value.Length switch
        {
            4 => true,
            7 => DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            10 => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
    }

    private static bool TryParseDateTimeOffsetAllowingLeapSecond(string value)
    {
        var normalized = NormalizeForDotNetDateTimeOffset(value);

        return DateTimeOffset.TryParse(
            normalized,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static string NormalizeForDotNetDateTimeOffset(string value)
    {
        var normalized = value.Contains(":60", StringComparison.Ordinal)
            ? value.Replace(":60", ":59", StringComparison.Ordinal)
            : value;

        var fractionStart = normalized.IndexOf('.', StringComparison.Ordinal);
        if (fractionStart < 0)
        {
            return normalized;
        }

        var fractionEnd = normalized.IndexOfAny(new[] { 'Z', '+', '-' }, fractionStart);
        if (fractionEnd < 0 || fractionEnd - fractionStart <= 8)
        {
            return normalized;
        }

        return normalized[..(fractionStart + 8)] + normalized[fractionEnd..];
    }

    [GeneratedRegex(@"^[0-9]{4}(-(0[1-9]|1[0-2])(-(0[1-9]|[12][0-9]|3[01])(T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]{1,9})?(Z|[+-]((0[0-9]|1[0-3]):[0-5][0-9]|14:00)))?)?)?$")]
    private static partial Regex DateTimeRegex();
}
