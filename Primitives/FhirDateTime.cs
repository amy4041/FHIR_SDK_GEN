using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR dateTime primitive. Supports partial dates and full date-times.
/// If a time is present, a timezone offset is required.
/// </summary>
public sealed class FhirDateTime : PrimitiveType<string>
{
    public FhirDateTime()
    {
    }

    public FhirDateTime(string? value)
        : base(value)
    {
    }

}
