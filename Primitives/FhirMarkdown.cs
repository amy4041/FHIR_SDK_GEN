using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

/// <summary>
/// FHIR markdown primitive. Markdown values are strings intended for markdown rendering.
/// </summary>
public sealed class FhirMarkdown : PrimitiveType<string>
{
    public const int MaxLength = 1024 * 1024;

    public FhirMarkdown()
    {
    }

    public FhirMarkdown(string? value)
        : base(value)
    {
    }

}
