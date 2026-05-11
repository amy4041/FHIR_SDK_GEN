using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Resources;

/// <summary>
/// Request metadata for a FHIR R4 Bundle entry.
/// </summary>
public sealed class BundleEntryRequest : BackboneElement
{
    /// <summary>
    /// GET | HEAD | POST | PUT | DELETE | PATCH.
    /// </summary>
    public FhirCode? Method { get; set; }

    /// <summary>
    /// Relative or absolute request URL.
    /// </summary>
    public FhirUri? Url { get; set; }

    /// <summary>
    /// ETag for conditional reads.
    /// </summary>
    public FhirString? IfNoneMatch { get; set; }

    /// <summary>
    /// Last modified instant for conditional reads.
    /// </summary>
    public FhirInstant? IfModifiedSince { get; set; }

    /// <summary>
    /// ETag for conditional updates.
    /// </summary>
    public FhirString? IfMatch { get; set; }

    /// <summary>
    /// Search criteria for conditional creates.
    /// </summary>
    public FhirString? IfNoneExist { get; set; }
}
