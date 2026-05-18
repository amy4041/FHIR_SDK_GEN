using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R4 Claim resource for requesting payment or authorization.
/// </summary>
public sealed class Claim : DomainResource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Claim";

    /// <summary>
    /// Business identifiers assigned to this claim.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// Tracking numbers associated with this claim.
    /// </summary>
    public IList<Identifier> TraceNumber { get; set; } = new List<Identifier>();

    /// <summary>
    /// active | cancelled | draft | entered-in-error.
    /// </summary>
    public FhirCode? Status { get; set; }

    /// <summary>
    /// Category of claim, such as institutional, oral, pharmacy, professional, or vision.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// More granular claim category.
    /// </summary>
    public CodeableConcept? SubType { get; set; }

    /// <summary>
    /// claim | preauthorization | predetermination.
    /// </summary>
    public FhirCode? Use { get; set; }

    /// <summary>
    /// Patient for whom products or services are claimed.
    /// </summary>
    public Reference? Patient { get; set; }

    /// <summary>
    /// Period for which the claim applies.
    /// </summary>
    public Period? BillablePeriod { get; set; }

    /// <summary>
    /// Date or time when the claim was created.
    /// </summary>
    public FhirDateTime? Created { get; set; }

    /// <summary>
    /// Party responsible for entering the claim.
    /// </summary>
    public Reference? Enterer { get; set; }

    /// <summary>
    /// Target insurer or payer.
    /// </summary>
    public Reference? Insurer { get; set; }

    /// <summary>
    /// Provider responsible for the claim.
    /// </summary>
    public Reference? Provider { get; set; }

    /// <summary>
    /// Desired processing priority.
    /// </summary>
    public CodeableConcept? Priority { get; set; }

    /// <summary>
    /// Funds reservation request.
    /// </summary>
    public CodeableConcept? FundsReserve { get; set; }

    /// <summary>
    /// Prior or corollary claims.
    /// </summary>
    public IList<ClaimRelated> Related { get; set; } = new List<ClaimRelated>();

    /// <summary>
    /// Prescription authorizing services and products.
    /// </summary>
    public Reference? Prescription { get; set; }

    /// <summary>
    /// Original prescription if superseded by a fulfiller.
    /// </summary>
    public Reference? OriginalPrescription { get; set; }

    /// <summary>
    /// Recipient of benefits payable.
    /// </summary>
    public ClaimPayee? Payee { get; set; }

    /// <summary>
    /// Treatment referral.
    /// </summary>
    public Reference? Referral { get; set; }

    /// <summary>
    /// Encounters associated with the listed treatments.
    /// </summary>
    public IList<Reference> Encounter { get; set; } = new List<Reference>();

    /// <summary>
    /// Servicing facility.
    /// </summary>
    public Reference? Facility { get; set; }

    /// <summary>
    /// Package billing code.
    /// </summary>
    public CodeableConcept? DiagnosisRelatedGroup { get; set; }

    /// <summary>
    /// Event information.
    /// </summary>
    public IList<ClaimEvent> Event { get; set; } = new List<ClaimEvent>();

    /// <summary>
    /// Members of the care team.
    /// </summary>
    public IList<ClaimCareTeam> CareTeam { get; set; } = new List<ClaimCareTeam>();

    /// <summary>
    /// Supporting information for the claim.
    /// </summary>
    public IList<ClaimSupportingInfo> SupportingInfo { get; set; } = new List<ClaimSupportingInfo>();

    /// <summary>
    /// Diagnoses related to the claim.
    /// </summary>
    public IList<ClaimDiagnosis> Diagnosis { get; set; } = new List<ClaimDiagnosis>();

    /// <summary>
    /// Procedures related to the claim.
    /// </summary>
    public IList<ClaimProcedure> Procedure { get; set; } = new List<ClaimProcedure>();

    /// <summary>
    /// Insurance coverages associated with the claim.
    /// </summary>
    public IList<ClaimInsurance> Insurance { get; set; } = new List<ClaimInsurance>();

    /// <summary>
    /// Accident details related to the claim.
    /// </summary>
    public ClaimAccident? Accident { get; set; }

    /// <summary>
    /// Amount paid by the patient.
    /// </summary>
    public Money? PatientPaid { get; set; }

    /// <summary>
    /// Products or services being claimed.
    /// </summary>
    public IList<ClaimItem> Item { get; set; } = new List<ClaimItem>();

    /// <summary>
    /// Total claim cost.
    /// </summary>
    public Money? Total { get; set; }
}
