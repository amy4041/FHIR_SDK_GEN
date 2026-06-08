using System;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR canonical primitive. Canonicals are absolute URIs or fragment references,
/// optionally with a version suffix separated by '|'.
/// </summary>
public sealed partial class FhirCanonical : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public FhirCanonical()
    {
    }

    public FhirCanonical(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        if (!NoWhitespaceRegex().IsMatch(Value))
        {
            return false;
        }

        var uriPart = Value.Split('|', 2)[0];
        return uriPart.Length == 0
            || uriPart.StartsWith('#')
            || Uri.TryCreate(uriPart, UriKind.Absolute, out _);
    }

    [GeneratedRegex(@"^\S*$")]
    private static partial Regex NoWhitespaceRegex();
}
