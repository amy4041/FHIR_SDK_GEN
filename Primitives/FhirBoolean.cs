using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR boolean primitive. Valid values are true or false when present.
/// </summary>
public sealed class FhirBoolean : PrimitiveType<bool?>
{
    public FhirBoolean()
    {
    }

    public FhirBoolean(bool? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        return true;
    }

    public override string ToString()
    {
        return Value?.ToString().ToLowerInvariant() ?? string.Empty;
    }
}
