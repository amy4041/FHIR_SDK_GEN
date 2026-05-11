using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Product or service line item on a FHIR R4 Claim.
/// </summary>
public sealed class ClaimItem : BackboneElement
{
    /// <summary>
    /// Item sequence number.
    /// </summary>
    public FhirInteger? Sequence { get; set; }

    /// <summary>
    /// Care team sequence numbers associated with this item.
    /// </summary>
    public IList<FhirInteger> CareTeamSequence { get; set; } = new List<FhirInteger>();

    /// <summary>
    /// Diagnosis sequence numbers associated with this item.
    /// </summary>
    public IList<FhirInteger> DiagnosisSequence { get; set; } = new List<FhirInteger>();

    /// <summary>
    /// Revenue or cost center code.
    /// </summary>
    public CodeableConcept? Revenue { get; set; }

    /// <summary>
    /// Product or service being claimed.
    /// </summary>
    public CodeableConcept? ProductOrService { get; set; }

    /// <summary>
    /// Date when the service was provided.
    /// </summary>
    public FhirDate? ServicedDate { get; set; }

    /// <summary>
    /// Period when the service was provided.
    /// </summary>
    public Period? ServicedPeriod { get; set; }

    /// <summary>
    /// Location where the service was provided, represented by a code.
    /// </summary>
    public CodeableConcept? LocationCodeableConcept { get; set; }

    /// <summary>
    /// Location where the service was provided, represented by a reference.
    /// </summary>
    public Reference? LocationReference { get; set; }

    /// <summary>
    /// Number of service units.
    /// </summary>
    public Quantity? Quantity { get; set; }

    /// <summary>
    /// Unit price. The MVP represents amount-like values with Quantity until Money is added.
    /// </summary>
    public Quantity? UnitPrice { get; set; }

    /// <summary>
    /// Line item amount. The MVP represents amount-like values with Quantity until Money is added.
    /// </summary>
    public Quantity? Net { get; set; }
}
