using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R4 Organization resource for formal and informal organizations.
/// </summary>
public sealed class Organization : DomainResource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Organization";

    /// <summary>
    /// Business identifiers assigned to this organization.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// Whether this organization's record is in active use.
    /// </summary>
    public FhirBoolean? Active { get; set; }

    /// <summary>
    /// Kinds of organization.
    /// </summary>
    public IList<CodeableConcept> Type { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Name used for the organization.
    /// </summary>
    public FhirString? Name { get; set; }

    /// <summary>
    /// Alternate names used for the organization.
    /// </summary>
    public IList<FhirString> Alias { get; set; } = new List<FhirString>();

    /// <summary>
    /// Contact details for the organization.
    /// </summary>
    public IList<ContactPoint> Telecom { get; set; } = new List<ContactPoint>();

    /// <summary>
    /// Addresses associated with the organization.
    /// </summary>
    public IList<Address> Address { get; set; } = new List<Address>();

    /// <summary>
    /// Parent organization.
    /// </summary>
    public Reference? PartOf { get; set; }

    /// <summary>
    /// Contact parties for the organization.
    /// </summary>
    public IList<OrganizationContact> Contact { get; set; } = new List<OrganizationContact>();
}
