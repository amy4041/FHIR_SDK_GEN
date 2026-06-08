using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR code primitive. Codes have no leading or trailing whitespace and may
/// contain only single spaces between non-whitespace runs.
/// </summary>
public sealed partial class FhirCode : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public FhirCode()
    {
    }

    public FhirCode(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        return Value is null || CodeRegex().IsMatch(Value);
    }

    [GeneratedRegex(@"^[^\s]+( [^\s]+)*$")]
    private static partial Regex CodeRegex();
}
