using System.Globalization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR integer primitive backed by a signed 32-bit integer.
/// </summary>
public sealed class FhirInteger : PrimitiveType<int?>
{
    public FhirInteger()
    {
    }

    public FhirInteger(int? value)
        : base(value)
    {
    }

    public override string ToString()
    {
        return Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
