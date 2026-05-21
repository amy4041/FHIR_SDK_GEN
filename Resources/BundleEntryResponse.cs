using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Resources;

/// <summary>
/// Response metadata for a FHIR R5 Bundle entry.
/// </summary>
public sealed class BundleEntryResponse : BackboneElement
{
    /// <summary>
    /// HTTP status code and text.
    /// </summary>
    public FhirString? Status { get; set; }

    /// <summary>
    /// Location header value.
    /// </summary>
    public FhirUri? Location { get; set; }

    /// <summary>
    /// ETag header value.
    /// </summary>
    public FhirString? Etag { get; set; }

    /// <summary>
    /// Last modified instant.
    /// </summary>
    public FhirInstant? LastModified { get; set; }

    /// <summary>
    /// OperationOutcome or other resource returned with the response.
    /// </summary>
    public Resource? Outcome { get; set; }
}
