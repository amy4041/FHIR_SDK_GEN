using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR base64Binary primitive. The value is base64 content without whitespace.
/// </summary>
public sealed class FhirBase64Binary : PrimitiveType<string>
{
    public FhirBase64Binary()
    {
    }

    public FhirBase64Binary(string? value)
        : base(value)
    {
    }

}
