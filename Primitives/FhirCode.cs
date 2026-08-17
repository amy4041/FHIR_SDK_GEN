using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR code primitive. Codes have no leading or trailing whitespace and may
/// contain only single spaces between non-whitespace runs.
/// </summary>
public sealed class FhirCode : PrimitiveType<string>
{
    public FhirCode()
    {
    }

    public FhirCode(string? value)
        : base(value)
    {
    }

}
