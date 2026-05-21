using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Insurance coverage information on a FHIR R5 Claim.
/// </summary>
public sealed class ClaimInsurance : BackboneElement
{
    /// <summary>
    /// Insurance sequence number.
    /// </summary>
    public FhirPositiveInt? Sequence { get; set; }

    /// <summary>
    /// Whether this coverage is the focal coverage for adjudication.
    /// </summary>
    public FhirBoolean? Focal { get; set; }

    /// <summary>
    /// Pre-assigned claim number.
    /// </summary>
    public Identifier? Identifier { get; set; }

    /// <summary>
    /// Coverage resource used for this claim.
    /// </summary>
    public Reference? Coverage { get; set; }

    /// <summary>
    /// Insurer-assigned business arrangement.
    /// </summary>
    public FhirString? BusinessArrangement { get; set; }

    /// <summary>
    /// Prior authorization reference numbers.
    /// </summary>
    public IList<FhirString> PreAuthRef { get; set; } = new List<FhirString>();

    /// <summary>
    /// Adjudication results.
    /// </summary>
    public Reference? ClaimResponse { get; set; }
}
