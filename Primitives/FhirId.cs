using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR id primitive. Allows ASCII letters, digits, '-' and '.', up to 64 chars.
/// </summary>
public sealed partial class FhirId : PrimitiveType<string>
{
    public FhirId()
    {
    }

    public FhirId(string? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        return Value is null || IdRegex().IsMatch(Value);
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-\.]{1,64}$")]
    private static partial Regex IdRegex();
}
