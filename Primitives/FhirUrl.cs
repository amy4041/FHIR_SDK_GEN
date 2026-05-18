using System;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR url primitive. URLs are directly accessed using their specified protocol.
/// </summary>
public sealed partial class FhirUrl : PrimitiveType<string>
{
    public FhirUrl()
    {
    }

    public FhirUrl(string? value)
        : base(value)
    {
    }

    public bool IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        return NoWhitespaceRegex().IsMatch(Value)
            && (Value.Length == 0 || Uri.TryCreate(Value, UriKind.Absolute, out _));
    }

    [GeneratedRegex(@"^\S*$")]
    private static partial Regex NoWhitespaceRegex();
}
