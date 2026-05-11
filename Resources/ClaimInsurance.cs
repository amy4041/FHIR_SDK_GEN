using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Insurance coverage information on a FHIR R4 Claim.
/// </summary>
public sealed class ClaimInsurance : BackboneElement
{
    /// <summary>
    /// Insurance sequence number.
    /// </summary>
    public FhirInteger? Sequence { get; set; }

    /// <summary>
    /// Whether this coverage is the focal coverage for adjudication.
    /// </summary>
    public FhirBoolean? Focal { get; set; }

    /// <summary>
    /// Coverage resource used for this claim.
    /// </summary>
    public Reference? Coverage { get; set; }

    /// <summary>
    /// Insurer-assigned business arrangement.
    /// </summary>
    public FhirString? BusinessArrangement { get; set; }
}
