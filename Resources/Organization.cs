using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R5 Organization resource for formal and informal organizations.
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
    /// Additional details that identify the organization beyond its name.
    /// </summary>
    public FhirMarkdown? Description { get; set; }

    /// <summary>
    /// Official contact details for the organization.
    /// </summary>
    public IList<ExtendedContactDetail> Contact { get; set; } = new List<ExtendedContactDetail>();

    /// <summary>
    /// Parent organization.
    /// </summary>
    public Reference? PartOf { get; set; }

    /// <summary>
    /// Technical endpoints providing access to services operated for the organization.
    /// </summary>
    public IList<Reference> Endpoint { get; set; } = new List<Reference>();

    /// <summary>
    /// Qualifications, certifications, accreditations, licenses, and training.
    /// </summary>
    public IList<OrganizationQualification> Qualification { get; set; } = new List<OrganizationQualification>();
}
