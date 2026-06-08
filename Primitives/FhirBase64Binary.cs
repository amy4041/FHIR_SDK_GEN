using System;
using System.Text.RegularExpressions;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR base64Binary primitive. The value is base64 content without whitespace.
/// </summary>
public sealed partial class FhirBase64Binary : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public FhirBase64Binary()
    {
    }

    public FhirBase64Binary(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        return Base64BinaryRegex().IsMatch(Value)
            && Convert.TryFromBase64String(Value, Array.Empty<byte>(), out _);
    }

    [GeneratedRegex(@"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$")]
    private static partial Regex Base64BinaryRegex();
}
