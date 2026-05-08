using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR code primitive. Codes have no leading or trailing whitespace and may
/// contain only single spaces between non-whitespace runs.
/// </summary>
public sealed partial class FhirCode : PrimitiveType<string>
{
    public FhirCode()
    {
    }

    public FhirCode(string? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        return Value is null || CodeRegex().IsMatch(Value);
    }

    [GeneratedRegex(@"^[^\s]+( [^\s]+)*$")]
    private static partial Regex CodeRegex();
}
