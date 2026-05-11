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
    /// Diagnoses related to the claim.
    /// </summary>
    public IList<ClaimDiagnosis> Diagnosis { get; set; } = new List<ClaimDiagnosis>();

    /// <summary>
    /// Insurance coverages associated with the claim.
    /// </summary>
    public IList<ClaimInsurance> Insurance { get; set; } = new List<ClaimInsurance>();

    /// <summary>
    /// Products or services being claimed.
    /// </summary>
    public IList<ClaimItem> Item { get; set; } = new List<ClaimItem>();

    /// <summary>
    /// Total claim amount. The MVP represents amount-like values with Quantity until Money is added.
    /// </summary>
    public Quantity? Total { get; set; }
}
