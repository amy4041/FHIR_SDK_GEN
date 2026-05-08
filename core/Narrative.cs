namespace MyFhirSdk.Core;

/// <summary>
/// Human-readable XHTML summary of a domain resource.
/// </summary>
public sealed class Narrative : DataType
{
    /// <summary>
    /// Narrative status, such as generated, extensions, additional, or empty.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Escaped XHTML div content.
    /// </summary>
    public string? Div { get; set; }
}
