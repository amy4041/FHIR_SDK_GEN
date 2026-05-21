using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Resources;

/// <summary>
/// Search metadata for a FHIR R5 Bundle entry.
/// </summary>
public sealed class BundleEntrySearch : BackboneElement
{
    /// <summary>
    /// match | include | outcome.
    /// </summary>
    public FhirCode? Mode { get; set; }

    /// <summary>
    /// Search ranking score.
    /// </summary>
    public FhirDecimal? Score { get; set; }
}
