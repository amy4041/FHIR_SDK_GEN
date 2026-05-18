using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 Attachment datatype for content in a format defined elsewhere.
/// </summary>
public sealed class Attachment : DataType
{
    /// <summary>
    /// MIME type of the content, with charset or other parameters where appropriate.
    /// </summary>
    public FhirCode? ContentType { get; set; }

    /// <summary>
    /// Human language of the content, expressed as a BCP-47 language tag.
    /// </summary>
    public FhirCode? Language { get; set; }

    /// <summary>
    /// Inline base64Binary data.
    /// </summary>
    public FhirBase64Binary? Data { get; set; }

    /// <summary>
    /// URL where the data can be found.
    /// </summary>
    public FhirUrl? Url { get; set; }

    /// <summary>
    /// Number of bytes of content, usually when a URL is provided.
    /// </summary>
    public FhirInteger64? Size { get; set; }

    /// <summary>
    /// SHA-1 hash of the data, base64 encoded.
    /// </summary>
    public FhirBase64Binary? Hash { get; set; }

    /// <summary>
    /// Label to display in place of the data.
    /// </summary>
    public FhirString? Title { get; set; }

    /// <summary>
    /// Date the attachment was first created.
    /// </summary>
    public FhirDateTime? Creation { get; set; }

    /// <summary>
    /// Height of the image in pixels.
    /// </summary>
    public FhirPositiveInt? Height { get; set; }

    /// <summary>
    /// Width of the image in pixels.
    /// </summary>
    public FhirPositiveInt? Width { get; set; }

    /// <summary>
    /// Number of frames if greater than 1.
    /// </summary>
    public FhirPositiveInt? Frames { get; set; }

    /// <summary>
    /// Length in seconds for audio or video content.
    /// </summary>
    public FhirDecimal? Duration { get; set; }

    /// <summary>
    /// Number of printed pages.
    /// </summary>
    public FhirPositiveInt? Pages { get; set; }
}
