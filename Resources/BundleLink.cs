using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Resources;

/// <summary>
/// Link associated with a FHIR R5 Bundle.
/// </summary>
public sealed class BundleLink : BackboneElement
{
    /// <summary>
    /// Relation type for the link.
    /// </summary>
    public FhirCode? Relation { get; set; }

    /// <summary>
    /// Link URL.
    /// </summary>
    public FhirUri? Url { get; set; }
}
