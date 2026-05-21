using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Product or service line item on a FHIR R5 Claim.
/// </summary>
public sealed class ClaimItem : BackboneElement
{
    /// <summary>
    /// Item sequence number.
    /// </summary>
    public FhirPositiveInt? Sequence { get; set; }

    /// <summary>
    /// Tracking numbers associated with this item.
    /// </summary>
    public IList<Identifier> TraceNumber { get; set; } = new List<Identifier>();

    /// <summary>
    /// Care team sequence numbers associated with this item.
    /// </summary>
    public IList<FhirPositiveInt> CareTeamSequence { get; set; } = new List<FhirPositiveInt>();

    /// <summary>
    /// Diagnosis sequence numbers associated with this item.
    /// </summary>
    public IList<FhirPositiveInt> DiagnosisSequence { get; set; } = new List<FhirPositiveInt>();

    /// <summary>
    /// Procedure sequence numbers associated with this item.
    /// </summary>
    public IList<FhirPositiveInt> ProcedureSequence { get; set; } = new List<FhirPositiveInt>();

    /// <summary>
    /// Supporting information sequence numbers associated with this item.
    /// </summary>
    public IList<FhirPositiveInt> InformationSequence { get; set; } = new List<FhirPositiveInt>();

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
    /// Request or referral for service.
    /// </summary>
    public IList<Reference> Request { get; set; } = new List<Reference>();

    /// <summary>
    /// Product or service billing modifiers.
    /// </summary>
    public IList<CodeableConcept> Modifier { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Program the product or service is provided under.
    /// </summary>
    public IList<CodeableConcept> ProgramCode { get; set; } = new List<CodeableConcept>();

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
    /// Location where the service was provided, represented by an address.
    /// </summary>
    public Address? LocationAddress { get; set; }

    /// <summary>
    /// Location where the service was provided, represented by a reference.
    /// </summary>
    public Reference? LocationReference { get; set; }

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
    /// Line item amount.
    /// </summary>
    public Money? Net { get; set; }

    /// <summary>
    /// Unique device identifiers associated with this line item.
    /// </summary>
    public IList<Reference> Udi { get; set; } = new List<Reference>();

    /// <summary>
    /// Anatomical locations.
    /// </summary>
    public IList<ClaimBodySite> BodySite { get; set; } = new List<ClaimBodySite>();

    /// <summary>
    /// Encounters associated with this item.
    /// </summary>
    public IList<Reference> Encounter { get; set; } = new List<Reference>();

    /// <summary>
    /// Product or service detail lines.
    /// </summary>
    public IList<ClaimDetail> Detail { get; set; } = new List<ClaimDetail>();
}
