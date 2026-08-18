using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR uri primitive. URIs may be absolute or relative and may include fragments.
/// </summary>
public sealed class FhirUri : PrimitiveType<string>
{
    public FhirUri()
    {
    }

    public FhirUri(string? value)
        : base(value)
    {
    }

}
