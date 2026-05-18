using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Product or service detail line for a FHIR R5 Claim item.
/// </summary>
public sealed class ClaimDetail : BackboneElement
{
    /// <summary>
    /// Detail sequence number.
    /// </summary>
    public FhirPositiveInt? Sequence { get; set; }

    /// <summary>
    /// Tracking numbers associated with this detail.
    /// </summary>
    public IList<Identifier> TraceNumber { get; set; } = new List<Identifier>();

    /// <summary>
    /// Revenue or cost center code.
    /// </summary>
    public CodeableConcept? Revenue { get; set; }

    /// <summary>
    /// Benefit classification.
    /// </summary>
    public CodeableConcept? Category { get; set; }

    /// <summary>
    /// Product or service being claimed.
    /// </summary>
    public CodeableConcept? ProductOrService { get; set; }

    /// <summary>
    /// End of a product or service code range.
    /// </summary>
    public CodeableConcept? ProductOrServiceEnd { get; set; }

    /// <summary>
    /// Service or product billing modifiers.
    /// </summary>
    public IList<CodeableConcept> Modifier { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Program the product or service is provided under.
    /// </summary>
    public IList<CodeableConcept> ProgramCode { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Amount paid by the patient.
    /// </summary>
    public Money? PatientPaid { get; set; }

    /// <summary>
    /// Number of service units.
    /// </summary>
    public SimpleQuantity? Quantity { get; set; }

    /// <summary>
    /// Fee, charge, or cost per item.
    /// </summary>
    public Money? UnitPrice { get; set; }

    /// <summary>
    /// Price scaling factor.
    /// </summary>
    public FhirDecimal? Factor { get; set; }

    /// <summary>
    /// Total tax.
    /// </summary>
    public Money? Tax { get; set; }

    /// <summary>
    /// Total detail cost.
    /// </summary>
    public Money? Net { get; set; }

    /// <summary>
    /// Unique device identifiers.
    /// </summary>
    public IList<Reference> Udi { get; set; } = new List<Reference>();

    /// <summary>
    /// Product or service sub-detail lines.
    /// </summary>
    public IList<ClaimSubDetail> SubDetail { get; set; } = new List<ClaimSubDetail>();
}
