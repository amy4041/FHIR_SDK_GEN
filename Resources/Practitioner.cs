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
    /// Indicates if the practitioner is deceased.
    /// </summary>
    public FhirBoolean? DeceasedBoolean { get; set; }

    /// <summary>
    /// Date or time of death.
    /// </summary>
    public FhirDateTime? DeceasedDateTime { get; set; }

    /// <summary>
    /// Images of the practitioner.
    /// </summary>
    public IList<Attachment> Photo { get; set; } = new List<Attachment>();

    /// <summary>
    /// Qualifications, certifications, or licenses held by the practitioner.
    /// </summary>
    public IList<PractitionerQualification> Qualification { get; set; } = new List<PractitionerQualification>();

    /// <summary>
    /// Languages that may be used to communicate with the practitioner.
    /// </summary>
    public IList<PractitionerCommunication> Communication { get; set; } = new List<PractitionerCommunication>();
}
