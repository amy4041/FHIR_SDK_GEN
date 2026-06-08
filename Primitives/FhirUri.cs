using System;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR uri primitive. URIs may be absolute or relative and may include fragments.
/// </summary>
public sealed partial class FhirUri : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public FhirUri()
    {
    }

    public FhirUri(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        return NoWhitespaceRegex().IsMatch(Value)
            && (Value.Length == 0 || Uri.TryCreate(Value, UriKind.RelativeOrAbsolute, out _));
    }

    [GeneratedRegex(@"^\S*$")]
    private static partial Regex NoWhitespaceRegex();
}
