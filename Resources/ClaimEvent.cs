using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Event information for a FHIR R5 Claim.
/// </summary>
public sealed class ClaimEvent : BackboneElement
{
    /// <summary>
    /// Specific event.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Event occurrence date/time.
    /// </summary>
    public FhirDateTime? WhenDateTime { get; set; }

    /// <summary>
    /// Event occurrence period.
    /// </summary>
    public Period? WhenPeriod { get; set; }
}
