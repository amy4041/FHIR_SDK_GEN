using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R4 Practitioner resource for a person involved in care delivery.
/// </summary>
public sealed class Practitioner : DomainResource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Practitioner";

    /// <summary>
    /// Business identifiers assigned to this practitioner.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// Whether this practitioner's record is in active use.
    /// </summary>
    public FhirBoolean? Active { get; set; }

    /// <summary>
    /// Names associated with the practitioner.
    /// </summary>
    public IList<HumanName> Name { get; set; } = new List<HumanName>();

    /// <summary>
    /// Contact details for the practitioner.
    /// </summary>
    public IList<ContactPoint> Telecom { get; set; } = new List<ContactPoint>();

    /// <summary>
    /// Addresses associated with the practitioner.
    /// </summary>
    public IList<Address> Address { get; set; } = new List<Address>();

    /// <summary>
    /// male | female | other | unknown.
    /// </summary>
    public FhirCode? Gender { get; set; }

    /// <summary>
    /// The practitioner's date of birth.
    /// </summary>
    public FhirDate? BirthDate { get; set; }

    /// <summary>
    /// Qualifications, certifications, or licenses held by the practitioner.
    /// </summary>
    public IList<PractitionerQualification> Qualification { get; set; } = new List<PractitionerQualification>();

    /// <summary>
    /// Languages used by the practitioner.
    /// </summary>
    public IList<CodeableConcept> Communication { get; set; } = new List<CodeableConcept>();
}
