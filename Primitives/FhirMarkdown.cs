using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR markdown primitive. Markdown values are strings intended for markdown rendering.
/// </summary>
public sealed class FhirMarkdown : PrimitiveType<string>, IFhirValidatablePrimitive
{
    public const int MaxLength = 1024 * 1024;

    public FhirMarkdown()
    {
    }

    public FhirMarkdown(string? value)
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
