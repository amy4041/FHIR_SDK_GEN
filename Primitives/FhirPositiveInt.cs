using System.Globalization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR positiveInt primitive. Valid values are 1 through 2,147,483,647.
/// </summary>
public sealed class FhirPositiveInt : PrimitiveType<int?>
{
    public FhirPositiveInt()
    {
    }

    public FhirPositiveInt(int? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        return Value is null or > 0;
    }

    public override string ToString()
    {
        return Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
