using System.Globalization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR integer64 primitive. JSON represents this value as a string to avoid
/// precision loss in floating point implementations.
/// </summary>
public sealed class FhirInteger64 : PrimitiveType<long?>
{
    public FhirInteger64()
    {
    }

    public FhirInteger64(long? value)
        : base(value)
    {
    }

    public FhirInteger64(string? value)
    {
        Literal = value;

        if (value is not null &&
            long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedValue))
        {
            Value = parsedValue;
        }
    }

    /// <summary>
    /// Original integer64 literal when the value was created from text.
    /// </summary>
    public string? Literal { get; }

    public override string ToString()
    {
        return Literal ?? Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

}
