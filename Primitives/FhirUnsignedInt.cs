using System.Globalization;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR unsignedInt primitive. Valid values are 0 through 2,147,483,647.
/// </summary>
public sealed class FhirUnsignedInt : PrimitiveType<int?>, IFhirValidatablePrimitive
{
    public FhirUnsignedInt()
    {
    }

    public FhirUnsignedInt(int? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        return Value is null or >= 0;
    }

    public override string ToString()
    {
        return Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
