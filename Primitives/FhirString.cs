using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR string primitive. Strings are limited to 1,048,576 characters.
/// </summary>
public sealed class FhirString : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public const int MaxLength = 1024 * 1024;

    public FhirString()
    {
    }

    public FhirString(string? value)
        : base(value)
    {
    }

    bool IFhirValidatablePrimitive.IsValid()
    {
        if (Value is null)
        {
            return true;
        }

        if (Value.Length is 0 or > MaxLength)
        {
            return false;
        }

        foreach (var character in Value)
        {
            if (character < 32 && character is not '\t' and not '\r' and not '\n')
            {
                return false;
            }
        }

        return true;
    }
}
