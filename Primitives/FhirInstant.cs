using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR instant primitive. Requires a full date-time at least to seconds and a timezone.
/// </summary>
public sealed partial class FhirInstant : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public FhirInstant()
    {
    }

    public FhirInstant(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        if (!InstantRegex().IsMatch(Value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            NormalizeForDotNetDateTimeOffset(Value),
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

    [GeneratedRegex(@"^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]{1,9})?(Z|[+-]((0[0-9]|1[0-3]):[0-5][0-9]|14:00))$")]
    private static partial Regex InstantRegex();
}
