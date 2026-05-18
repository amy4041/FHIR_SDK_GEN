using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Resources;

/// <summary>
/// Entry contained in a FHIR R4 Bundle.
/// </summary>
public sealed class BundleEntry : BackboneElement
{
    /// <summary>
    /// Links related to this entry.
    /// </summary>
    public IList<BundleLink> Link { get; set; } = new List<BundleLink>();

    /// <summary>
    /// URI for the resource in the entry.
    /// </summary>
    public FhirUri? FullUrl { get; set; }

    /// <summary>
    /// Resource contained in the entry.
    /// </summary>
    public Resource? Resource { get; set; }

    /// <summary>
    /// Search metadata for search result bundles.
    /// </summary>
    public BundleEntrySearch? Search { get; set; }

    /// <summary>
    /// Transaction or batch request details.
    /// </summary>
    public BundleEntryRequest? Request { get; set; }

    /// <summary>
    /// Transaction or batch response details.
    /// </summary>
    public BundleEntryResponse? Response { get; set; }
}
