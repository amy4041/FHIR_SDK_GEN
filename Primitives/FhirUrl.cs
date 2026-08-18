using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR url primitive. URLs are directly accessed using their specified protocol.
/// </summary>
public sealed class FhirUrl : PrimitiveType<string>
{
    public FhirUrl()
    {
    }

    public FhirUrl(string? value)
        : base(value)
    {
    }

}
