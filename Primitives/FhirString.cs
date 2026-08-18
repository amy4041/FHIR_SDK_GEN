using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR string primitive. Strings are limited to 1,048,576 characters.
/// </summary>
public sealed class FhirString : PrimitiveType<string>
{
    public const int MaxLength = 1024 * 1024;

    public FhirString()
    {
    }

    public FhirString(string? value)
        : base(value)
    {
    }

}
