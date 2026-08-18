using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR canonical primitive. Canonicals are absolute URIs or fragment references,
/// optionally with a version suffix separated by '|'.
/// </summary>
public sealed class FhirCanonical : PrimitiveType<string>
{
    public FhirCanonical()
    {
    }

    public FhirCanonical(string? value)
        : base(value)
    {
    }

}
