using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R5 Patient resource for demographic and administrative patient data.
/// </summary>
public sealed class Patient : DomainResource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Patient";

    /// <summary>
    /// Business identifiers assigned to this patient.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// Whether this patient's record is in active use.
    /// </summary>
    public FhirBoolean? Active { get; set; }

    /// <summary>
    /// Names associated with the patient.
    /// </summary>
    public IList<HumanName> Name { get; set; } = new List<HumanName>();

    /// <summary>
    /// Contact details for the patient.
    /// </summary>
    public IList<ContactPoint> Telecom { get; set; } = new List<ContactPoint>();

    /// <summary>
    /// male | female | other | unknown.
    /// </summary>
    public FhirCode? Gender { get; set; }

    /// <summary>
    /// The patient's date of birth.
    /// </summary>
    public FhirDate? BirthDate { get; set; }

    /// <summary>
    /// Indicates if the patient is deceased.
    /// </summary>
    public FhirBoolean? DeceasedBoolean { get; set; }

    /// <summary>
    /// Date or time of death.
    /// </summary>
    public FhirDateTime? DeceasedDateTime { get; set; }

    /// <summary>
    /// Addresses associated with the patient.
    /// </summary>
    public IList<Address> Address { get; set; } = new List<Address>();

    /// <summary>
    /// Marital or civil status.
    /// </summary>
    public CodeableConcept? MaritalStatus { get; set; }

    /// <summary>
    /// Whether the patient was part of a multiple birth.
    /// </summary>
    public FhirBoolean? MultipleBirthBoolean { get; set; }

    /// <summary>
    /// Birth order when the patient was part of a multiple birth.
    /// </summary>
    public FhirInteger? MultipleBirthInteger { get; set; }

    /// <summary>
    /// Contacts such as guardians, partners, or friends.
    /// </summary>
    public IList<PatientContact> Contact { get; set; } = new List<PatientContact>();

    /// <summary>
    /// Languages used to communicate with the patient.
    /// </summary>
    public IList<PatientCommunication> Communication { get; set; } = new List<PatientCommunication>();

    /// <summary>
    /// Care providers nominated for this patient.
    /// </summary>
    public IList<Reference> GeneralPractitioner { get; set; } = new List<Reference>();

    /// <summary>
    /// Organization responsible for maintaining this patient record.
    /// </summary>
    public Reference? ManagingOrganization { get; set; }
}
