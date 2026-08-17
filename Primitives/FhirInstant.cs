using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR instant primitive. Requires a full date-time at least to seconds and a timezone.
/// </summary>
public sealed class FhirInstant : PrimitiveType<string>
{
    public FhirInstant()
    {
    }

    public FhirInstant(string? value)
        : base(value)
    {
    }

}
