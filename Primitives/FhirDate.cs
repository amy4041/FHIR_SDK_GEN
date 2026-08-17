using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR date primitive. Supports YYYY, YYYY-MM, and YYYY-MM-DD with no timezone.
/// </summary>
public sealed class FhirDate : PrimitiveType<string>
{
    public FhirDate()
    {
    }

    public FhirDate(string? value)
        : base(value)
    {
    }

}
