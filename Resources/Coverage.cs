using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R5 Coverage resource for insurance or other payment coverage.
/// </summary>
public sealed class Coverage : DomainResource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Coverage";

    /// <summary>
    /// Identifiers assigned to this coverage.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// active | cancelled | draft | entered-in-error.
    /// </summary>
    public FhirCode? Status { get; set; }

    /// <summary>
    /// insurance | self-pay | other.
    /// </summary>
    public FhirCode? Kind { get; set; }

    /// <summary>
    /// Self-pay parties and responsibilities.
    /// </summary>
    public IList<CoveragePaymentBy> PaymentBy { get; set; } = new List<CoveragePaymentBy>();

    /// <summary>
    /// Type of coverage.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Party who owns the policy.
    /// </summary>
    public Reference? PolicyHolder { get; set; }

    /// <summary>
    /// Party who has signed up for the policy.
    /// </summary>
    public Reference? Subscriber { get; set; }

    /// <summary>
    /// Insurer-assigned subscriber identifier.
    /// </summary>
    public IList<Identifier> SubscriberId { get; set; } = new List<Identifier>();

    /// <summary>
    /// Party covered by the policy.
    /// </summary>
    public Reference? Beneficiary { get; set; }

    /// <summary>
    /// Dependent number on the policy.
    /// </summary>
    public FhirString? Dependent { get; set; }

    /// <summary>
    /// Relationship of beneficiary to subscriber.
    /// </summary>
    public CodeableConcept? Relationship { get; set; }

    /// <summary>
    /// Time period when the coverage is in force.
    /// </summary>
    public Period? Period { get; set; }

    /// <summary>
    /// Issuer or payer for this coverage.
    /// </summary>
    public Reference? Insurer { get; set; }

    /// <summary>
    /// Coverage classification such as group, plan, or class.
    /// </summary>
    public IList<CoverageClass> Class { get; set; } = new List<CoverageClass>();

    /// <summary>
    /// Relative order of this coverage.
    /// </summary>
    public FhirPositiveInt? Order { get; set; }

    /// <summary>
    /// Insurer network.
    /// </summary>
    public FhirString? Network { get; set; }

    /// <summary>
    /// Patient payments for services or products.
    /// </summary>
    public IList<CoverageCostToBeneficiary> CostToBeneficiary { get; set; } = new List<CoverageCostToBeneficiary>();

    /// <summary>
    /// Whether this coverage is included only for insurer reimbursement recovery.
    /// </summary>
    public FhirBoolean? Subrogation { get; set; }

    /// <summary>
    /// Contract details.
    /// </summary>
    public IList<Reference> Contract { get; set; } = new List<Reference>();

    /// <summary>
    /// Insurance plan details.
    /// </summary>
    public Reference? InsurancePlan { get; set; }
}
