using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R4 Coverage resource for insurance or other payment coverage.
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
    public FhirString? SubscriberId { get; set; }

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
    public IList<Reference> Payor { get; set; } = new List<Reference>();

    /// <summary>
    /// Coverage classification such as group, plan, or class.
    /// </summary>
    public IList<CoverageClass> Class { get; set; } = new List<CoverageClass>();

    /// <summary>
    /// Relative order of this coverage.
    /// </summary>
    public FhirInteger? Order { get; set; }

    /// <summary>
    /// Insurer network.
    /// </summary>
    public FhirString? Network { get; set; }
}
