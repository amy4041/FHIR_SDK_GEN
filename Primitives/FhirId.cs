using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR id primitive. Allows ASCII letters, digits, '-' and '.', up to 64 chars.
/// </summary>
public sealed class FhirId : PrimitiveType<string>
{
    public FhirId()
    {
    }

    public FhirId(string? value)
        : base(value)
    {
    }

}
