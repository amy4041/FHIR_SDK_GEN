using System.Globalization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR decimal primitive backed by the .NET decimal type.
/// </summary>
public sealed class FhirDecimal : PrimitiveType<decimal>
{
    public FhirDecimal()
    {
    }

    public FhirDecimal(decimal? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        return true;
    }

    public override string ToString()
    {
        return Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
